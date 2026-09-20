using SkiaSharp;

namespace probe;

/// <summary>Isolates which SkiaSharp call crashes: runtime effect, uniforms or child shader.</summary>
public static class Diag
{
    private static void Step(string name)
    {
        Console.WriteLine("STEP: " + name);
        Console.Out.Flush();
    }

    private static void DrawWith(SKShader shader)
    {
        var info = new SKImageInfo(64, 64);
        using var bmp = new SKBitmap(info);
        using var canvas = new SKCanvas(bmp);
        canvas.DrawRoundRect(new SKRect(0, 0, 64, 64), 8, 8, new SKPaint { Shader = shader });
    }

    public static void Run()
    {
        var info = new SKImageInfo(64, 64);

        Step("plain rounded rect");
        using (var bmp = new SKBitmap(info))
        using (var canvas = new SKCanvas(bmp))
            canvas.DrawRoundRect(new SKRect(0, 0, 64, 64), 8, 8, new SKPaint { Color = SKColors.Red });
        Console.WriteLine("  ok");

        Step("runtime effect: no uniforms / no children");
        var e1 = SKRuntimeEffect.Create("half4 main(float2 p) { return half4(1,0,0,1); }", out var err1);
        Console.WriteLine("  compile: " + (e1 is null ? "FAIL " + err1 : "ok"));
        if (e1 is not null)
        {
            using var s1 = e1.ToShader(false);
            Console.WriteLine("  toShader: " + (s1 is null ? "null" : "ok"));
            DrawWith(s1);
            Console.WriteLine("  draw ok");
        }

        Step("runtime effect: float2/float4/float uniforms");
        var e2 = SKRuntimeEffect.Create(
            "uniform float2 size; uniform float4 radii; uniform float amount;" +
            "half4 main(float2 p) { return half4(radii.x, amount, size.x / 64.0, 1); }", out var err2);
        Console.WriteLine("  compile: " + (e2 is null ? "FAIL " + err2 : "ok"));
        if (e2 is not null)
        {
            var u2 = new SKRuntimeEffectUniforms(e2);
            u2["size"] = new[] { 64f, 64f };
            u2["radii"] = new[] { 0.5f, 0f, 0f, 1f };
            u2["amount"] = 0.25f;
            using var s2 = e2.ToShader(false, u2);
            Console.WriteLine("  toShader: " + (s2 is null ? "null" : "ok"));
            DrawWith(s2);
            Console.WriteLine("  draw ok");
        }

        Step("runtime effect: uniform shader child + sample()");
        var e3 = SKRuntimeEffect.Create(
            "uniform shader content; half4 main(float2 p) { return sample(content, p); }", out var err3);
        Console.WriteLine("  compile: " + (e3 is null ? "FAIL " + err3 : "ok"));
        if (e3 is not null)
        {
            using var child = SKShader.CreateColor(new SKColor(0, 200, 255));
            var c3 = new SKRuntimeEffectChildren(e3);
            c3["content"] = child;
            using var s3 = e3.ToShader(false, null, c3);
            Console.WriteLine("  toShader: " + (s3 is null ? "null" : "ok"));
            DrawWith(s3);
            Console.WriteLine("  draw ok");
        }

        Step("runtime effect: child = image shader with local matrix");
        if (e3 is not null)
        {
            using var bmp = new SKBitmap(64, 64);
            using (var c = new SKCanvas(bmp)) c.Clear(SKColors.Green);
            using var img = SKImage.FromBitmap(bmp);
            using var child = SKShader.CreateImage(img, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp,
                SKMatrix.CreateTranslation(-10, -10));
            var c4 = new SKRuntimeEffectChildren(e3);
            c4["content"] = child;
            using var s4 = e3.ToShader(false, null, c4);
            Console.WriteLine("  toShader: " + (s4 is null ? "null" : "ok"));
            DrawWith(s4);
            Console.WriteLine("  draw ok");
        }

        Step("full ported shader: compile + uniforms + child + draw");
        var e5 = SKRuntimeEffect.Create(Shader.Sksl, out var err5);
        Console.WriteLine("  compile: " + (e5 is null ? "FAIL " + err5 : "ok"));
        if (e5 is not null)
        {
            using var bmp = new SKBitmap(64, 64);
            using (var c = new SKCanvas(bmp))
            {
                using var p = new SKPaint { Color = SKColors.Yellow };
                c.DrawRect(0, 0, 64, 64, p);
            }
            using var img = SKImage.FromBitmap(bmp);
            var u5 = new SKRuntimeEffectUniforms(e5);
            u5["size"] = new[] { 64f, 64f };
            u5["offset"] = new[] { 0f, 0f };
            u5["cornerRadii"] = new[] { 8f, 8f, 8f, 8f };
            u5["refractionHeight"] = 10f;
            u5["refractionAmount"] = -20f;
            u5["depthEffect"] = 0.3f;
            u5["chromaticAberration"] = 0.5f;
            u5["contrast"] = 0f;
            u5["whitePoint"] = 0f;
            u5["chromaMultiplier"] = 1f;
            u5["tintColor"] = new[] { 1f, 1f, 1f };
            u5["tintAlpha"] = 0f;
            var c5 = new SKRuntimeEffectChildren(e5);
            c5["content"] = SKShader.CreateImage(img, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
            using var s5 = e5.ToShader(false, u5, c5);
            Console.WriteLine("  toShader: " + (s5 is null ? "null" : "ok"));
            DrawWith(s5);
            Console.WriteLine("  draw ok");
        }

        Console.WriteLine("DIAG DONE");
    }
}
