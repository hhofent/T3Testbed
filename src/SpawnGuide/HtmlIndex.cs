using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpawnGuide
{

public sealed class HtmlSection
{
    public string Title { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Svg { get; set; } = "";
    public List<(string Label, string Value)> Facts { get; } = new();
}

public static class HtmlIndex
{
    /// <summary>Self-contained index page with every generated plan inlined.</summary>
    public static string Render(IReadOnlyList<HtmlSection> sections)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang='en'><head><meta charset='utf-8'>");
        sb.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1'>");
        sb.AppendLine("<title>Spawner Guide — 1990s Rural Iowa</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{margin:0;background:#221f1a;color:#e8e2d4;font-family:Georgia,serif}");
        sb.AppendLine("header{padding:28px 40px 8px;border-bottom:1px solid #3a352c}");
        sb.AppendLine("h1{font-size:22px;margin:0 0 6px;color:#f2ecdd}");
        sb.AppendLine(".sub{color:#a89f8d;font-size:13px;max-width:900px;line-height:1.5}");
        sb.AppendLine("main{display:flex;flex-direction:column;gap:28px;padding:28px 40px 60px}");
        sb.AppendLine("section{background:#2a2721;border:1px solid #3a352c;border-radius:8px;padding:18px;max-width:1100px}");
        sb.AppendLine("h2{font-size:17px;margin:0 0 4px;color:#f2ecdd;display:flex;justify-content:space-between;align-items:baseline;gap:16px}");
        sb.AppendLine(".seed{color:#a89f8d;font-size:12px;font-family:Consolas,monospace}");
        sb.AppendLine(".facts{display:flex;flex-wrap:wrap;gap:6px 18px;color:#c9c0ae;font-size:12px;margin:6px 0 12px;font-family:Consolas,monospace}");
        sb.AppendLine(".facts b{color:#8f8672;font-weight:normal}");
        sb.AppendLine(".plan{background:#faf7ef;border-radius:6px;overflow:auto;text-align:center}");
        sb.AppendLine(".plan svg{max-width:100%;height:auto}");
        sb.AppendLine("footer{padding:16px 40px 40px;color:#6f675a;font-size:11px}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<header><h1>SPAWNER GUIDE — 1990s Rural Iowa</h1>");
        sb.AppendLine("<p class='sub'>Seeded procedural plans for the building-construction spawner. Walls spawn ");
        sb.AppendLine("already fastened (framed per Builder-Guy: 2&times;6 plates, precut 92-5/8&Prime; studs, 97-1/8&Prime; framed height ");
        sb.AppendLine("per story); this page shows where they go and what is around them. Not a construction simulation.</p></header>");
        sb.AppendLine("<main>");
        foreach (var s in sections)
        {
            sb.AppendLine($"<section><h2>{Esc(s.Title)} <span class='seed'>{Esc(s.FileName)}</span></h2>");
            if (s.Facts.Count > 0)
            {
                sb.AppendLine("<div class='facts'>");
                foreach (var (label, value) in s.Facts)
                    sb.AppendLine($"<span><b>{Esc(label)}</b> {Esc(value)}</span>");
                sb.AppendLine("</div>");
            }
            sb.AppendLine($"<div class='plan'>{s.Svg}</div></section>");
        }
        sb.AppendLine("</main><footer>t3 testbed — spawn-guide experiments. Engine-agnostic C#; port to Unity after the rules settle.</footer></body></html>");
        return sb.ToString();
    }

    static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
}
