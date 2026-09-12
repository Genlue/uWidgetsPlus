using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Threading;

namespace Folders.Services;

/// <summary>
/// Registers a custom OLE drag-drop target on a window and forwards dropped files
/// to the UI thread. Used because the host application does not register an OLE
/// drop target on its windows (WinUIComposition mode disables the built-in one).
/// </summary>
public static class WidgetOleDropTarget
{
    private const short CF_HDROP = 15;
    private const uint DVASPECT_CONTENT = 1;
    private const int TYMED_HGLOBAL = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct FORMATETC
    {
        public short cfFormat;
        public IntPtr ptd;
        public uint dwAspect;
        public int lindex;
        public int tymed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STGMEDIUM
    {
        public int tymed;
        public IntPtr hGlobal;
        public IntPtr pUnkForRelease;
    }

    [ComImport, Guid("0000010E-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataObject
    {
        [PreserveSig] int GetData(ref FORMATETC pFormatEtc, out STGMEDIUM pmedium);
        [PreserveSig] int GetDataHere(ref FORMATETC pFormatEtc, ref STGMEDIUM pmedium);
        [PreserveSig] int QueryGetData(ref FORMATETC pFormatEtc);
        [PreserveSig] int GetCanonicalFormatEtc(ref FORMATETC pFormatEtcIn, out FORMATETC pFormatEtcOut);
        [PreserveSig] int SetData(ref FORMATETC pFormatEtc, ref STGMEDIUM pmedium, bool fRelease);
        [PreserveSig] int EnumFormatEtc(int dwDirection, out IntPtr ppenumFormatEtc);
        [PreserveSig] int DAdvise(ref FORMATETC pFormatEtc, int advf, IntPtr pAdvSink, out uint pdwConnection);
        [PreserveSig] int DUnadvise(uint dwConnection);
        [PreserveSig] int EnumDAdvise(out IntPtr ppenumAdvise);
    }

    [ComImport, Guid("00000118-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDropTarget
    {
        [PreserveSig] int DragEnter([In, MarshalAs(UnmanagedType.Interface)] IDataObject pDataObj, int grfKeyState, long pt, ref int pdwEffect);
        [PreserveSig] int DragOver(int grfKeyState, long pt, ref int pdwEffect);
        [PreserveSig] int DragLeave();
        [PreserveSig] int Drop([In, MarshalAs(UnmanagedType.Interface)] IDataObject pDataObj, int grfKeyState, long pt, ref int pdwEffect);
    }

    [DllImport("ole32.dll")]
    private static extern int RegisterDragDrop(IntPtr hWnd, [MarshalAs(UnmanagedType.Interface)] IDropTarget pDropTarget);

    [DllImport("ole32.dll")]
    private static extern int RevokeDragDrop(IntPtr hWnd);

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);

    [DllImport("shell32.dll")]
    private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

    private class DropTargetImpl : IDropTarget
    {
        private readonly Action<List<string>> onDrop;
        private readonly Action<bool> onDragActive;

        public DropTargetImpl(Action<List<string>> onDrop, Action<bool> onDragActive)
        {
            this.onDrop = onDrop;
            this.onDragActive = onDragActive;
        }

        public int DragEnter(IDataObject pDataObj, int grfKeyState, long pt, ref int pdwEffect)
        {
            if (HasFiles(pDataObj))
            {
                pdwEffect = 1; // DROPEFFECT_COPY
                onDragActive(true);
                return 0;
            }
            pdwEffect = 0;
            return 0;
        }

        public int DragOver(int grfKeyState, long pt, ref int pdwEffect)
        {
            pdwEffect = 1; // DROPEFFECT_COPY
            return 0;
        }

        public int DragLeave()
        {
            onDragActive(false);
            return 0;
        }

        public int Drop(IDataObject pDataObj, int grfKeyState, long pt, ref int pdwEffect)
        {
            onDragActive(false);
            var paths = ExtractFiles(pDataObj);
            if (paths.Count > 0)
                Dispatcher.UIThread.Post(() => onDrop(paths));
            pdwEffect = 1;
            return 0;
        }

        private static bool HasFiles(IDataObject data)
        {
            var fmt = new FORMATETC { cfFormat = CF_HDROP, ptd = IntPtr.Zero, dwAspect = DVASPECT_CONTENT, lindex = -1, tymed = TYMED_HGLOBAL };
            return data.QueryGetData(ref fmt) == 0;
        }

        private static List<string> ExtractFiles(IDataObject data)
        {
            var result = new List<string>();
            var fmt = new FORMATETC { cfFormat = CF_HDROP, ptd = IntPtr.Zero, dwAspect = DVASPECT_CONTENT, lindex = -1, tymed = TYMED_HGLOBAL };
            var medium = new STGMEDIUM();

            if (data.GetData(ref fmt, out medium) != 0) return result;
            try
            {
                if (medium.tymed != TYMED_HGLOBAL || medium.hGlobal == IntPtr.Zero) return result;

                var count = DragQueryFile(medium.hGlobal, 0xFFFFFFFF, null, 0);
                for (uint i = 0; i < count; i++)
                {
                    var length = DragQueryFile(medium.hGlobal, i, null, 0);
                    if (length == 0) continue;
                    var sb = new StringBuilder((int)length + 1);
                    DragQueryFile(medium.hGlobal, i, sb, length + 1);
                    result.Add(sb.ToString());
                }
            }
            finally
            {
                ReleaseStgMedium(ref medium);
            }
            return result;
        }
    }

    private static readonly Dictionary<IntPtr, DropTargetImpl> registrations = new();
    private static readonly Dictionary<IntPtr, SubclassState> subclassRegistrations = new();

    private const uint WM_DROPFILES = 0x0233;
    private static readonly IntPtr SubclassId = new(0x4655); // "FU"
    private static SubclassProc? subclassProc; // keep delegate alive

    private sealed class SubclassState
    {
        public Action<List<string>> OnDrop = _ => { };
        public Action<bool> OnDragActive = _ => { };
    }

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);
    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);
    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass);
    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(IntPtr hWnd, bool fAccept);
    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr hDrop);

    private static IntPtr SubclassProcImpl(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (uMsg == WM_DROPFILES && subclassRegistrations.TryGetValue(hWnd, out var state))
        {
            var paths = ReadDropFiles(wParam);
            DragFinish(wParam);
            if (paths.Count > 0)
                Dispatcher.UIThread.Post(() => state.OnDrop(paths));
            return IntPtr.Zero;
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam, uIdSubclass, dwRefData);
    }

    private static List<string> ReadDropFiles(IntPtr hDrop)
    {
        var result = new List<string>();
        var count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
        for (uint i = 0; i < count; i++)
        {
            var length = DragQueryFile(hDrop, i, null, 0);
            if (length == 0) continue;
            var sb = new StringBuilder((int)length + 1);
            DragQueryFile(hDrop, i, sb, length + 1);
            result.Add(sb.ToString());
        }
        return result;
    }

    /// <summary>
    /// Register the legacy WM_DROPFILES fallback. Works when OLE registration is unavailable.
    /// </summary>
    public static void RegisterWmDropFiles(IntPtr hwnd, Action<List<string>> onDrop, Action<bool> onDragActive)
    {
        if (hwnd == IntPtr.Zero) return;
        if (subclassRegistrations.TryGetValue(hwnd, out var existing))
        {
            existing.OnDrop = onDrop;
            existing.OnDragActive = onDragActive;
            return;
        }

        subclassProc ??= SubclassProcImpl;
        DragAcceptFiles(hwnd, true);
        if (SetWindowSubclass(hwnd, subclassProc, (UIntPtr)SubclassId, UIntPtr.Zero))
            subclassRegistrations[hwnd] = new SubclassState { OnDrop = onDrop, OnDragActive = onDragActive };
    }

    /// <summary>
    /// Unregister the WM_DROPFILES fallback.
    /// </summary>
    public static void UnregisterWmDropFiles(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        if (subclassRegistrations.Remove(hwnd) && subclassProc != null)
        {
            RemoveWindowSubclass(hwnd, subclassProc, (UIntPtr)SubclassId);
            DragAcceptFiles(hwnd, false);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    /// <summary>
    /// Register the drop target on a window handle. Unregisters any previous registration.
    /// </summary>
    public static void Register(IntPtr hwnd, Action<List<string>> onDrop, Action<bool> onDragActive)
    {
        if (hwnd == IntPtr.Zero) return;

        var root = GetRootWindow(hwnd);

        var ole = OleInitialize(IntPtr.Zero);

        // The host (Avalonia) registers its own drop target at window creation.
        // On this system a second RegisterDragDrop returns INVALIDHWND instead of
        // ALREADYREGISTERED, so we must revoke first to take over the target.
        var revoke = RevokeDragDrop(root);

        var target = new DropTargetImpl(onDrop, onDragActive);
        var hr = RegisterDragDrop(root, target);
        if (hr == 0)
        {
            registrations[root] = target;
        }
    }

    /// <summary>
    /// Unregister the drop target from a window handle.
    /// </summary>
    public static void Unregister(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        var root = GetRootWindow(hwnd);
        if (registrations.Remove(root))
            RevokeDragDrop(root);
    }

    private static IntPtr GetRootWindow(IntPtr hwnd)
    {
        var root = GetAncestor(hwnd, 2); // GA_ROOT
        return root == IntPtr.Zero ? hwnd : root;
    }
}