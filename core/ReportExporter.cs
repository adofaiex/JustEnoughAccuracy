using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Exports the current run's judgements as a UTF-8 YAML report.
    /// </summary>
    public static class ReportExporter
    {
        /// <summary>File name pattern: {prefix}{yyyyMMdd_HHmmss}{ext} (local time).</summary>
        public static string MakeFileName(string prefix, string ext)
        {
            return prefix + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ext;
        }

        public static string Build(string levelName)
        {
            var sb = new StringBuilder(4096);
            sb.Append("Timestamp: ")
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                .AppendLine();
            sb.Append("Level: ").AppendLine(Escape(levelName));
            sb.AppendLine();
            sb.AppendLine("Judgements:");

            foreach (var r in JudgementRecorder.Snapshot())
            {
                sb.AppendLine("  - Tile: " + r.Tile.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("    Timestamp: " + FormatTimestamp(r.Timestamp));
                sb.AppendLine("    RawDeviationDeg: " + FormatNum(r.RawDeviationDeg));
                sb.AppendLine("    DeviationMs: " + FormatNum(r.NormalizedDeviationDeg));
                sb.AppendLine("    NormalizedDeviationDeg: " + FormatNum(r.NormalizedDeviationDeg));
                sb.AppendLine("    Margin: " + HitMarginCompat.DisplayName(r.Margin));
                sb.AppendLine("    Acc: " + FormatPct(r.Acc));
                sb.AppendLine("    XAcc: " + FormatPct(r.XAcc));
                sb.AppendLine("    OfficialScore: " + (r.OfficialScore?.ToString(CultureInfo.InvariantCulture) ?? "null"));
                sb.AppendLine("    JeaTileScore: " + r.JeaTileScore.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("    JeaFinalTileScore: " + r.JeaFinalTileScore.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("    JeaTotalScore: " + r.JeaTotalScore.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("    JeaAccuracy: " + FormatAccuracy(r.JeaAccuracy));
                sb.AppendLine("    Combo: " + r.Combo.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("    NeaScore: " + (r.NeaScore?.ToString(CultureInfo.InvariantCulture) ?? "null"));
                sb.AppendLine("    NeaAcc: " + (r.NeaAcc?.ToString("0.###", CultureInfo.InvariantCulture) ?? "null"));
            }

            return sb.ToString();
        }

        /// <summary>Write the YAML report to the mod's own folder (UTF-8, no BOM).</summary>
        public static string WriteYaml(string levelName, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            var path = Path.Combine(outputDirectory, MakeFileName("JEA_report_", ".yaml"));
            File.WriteAllText(path, Build(levelName), new UTF8Encoding(false));
            return path;
        }

        /// <summary>Chart series menu: id → i18n key, stroke color, right-axis flag.</summary>
        public static readonly (string Id, string I18nKey, string Color, bool Right)[] ChartSeries =
        {
            ("jeaAcc",   "chart.jeaAcc",   "#7CE0B3", false),
            ("neaAcc",   "chart.neaAcc",   "#8AA7FF", false),
            ("jeaScore", "chart.jeaScore", "#F3D98B", false),
            ("neaScore", "chart.neaScore", "#C9A0FF", false),
            ("acc",      "chart.acc",      "#E8F0FF", false),
            ("xacc",     "chart.xacc",     "#FFDA00", false),
            ("dev",      "chart.dev",      "#E08A7C", true),
        };

        /// <summary>
        /// Write an HTML file with an inline SVG line chart of the selected
        /// series (default all). Left axis is 0-100 (scores / accuracy percents),
        /// right axis is signed deviation in ms (0 centered; early below, late above).
        /// Points carry native tooltips; NEA gaps
        /// (no data / noop codes) break the line instead of plotting zero.
        /// </summary>
        public static string WriteChartHtml(string levelName, string outputDirectory,
            ICollection<string>? selected = null)
        {
            Directory.CreateDirectory(outputDirectory);
            var path = Path.Combine(outputDirectory, MakeFileName("JEA_chart_", ".html"));
            File.WriteAllText(path, BuildChartHtml(levelName, selected), new UTF8Encoding(false));
            return path;
        }

        private static string BuildChartHtml(string levelName, ICollection<string>? selected)
        {
            var records = JudgementRecorder.Snapshot();
            var n = records.Count;
            var getters = ChartGetters();
            var active = new List<(string Id, string Label, string Color, bool Right, Func<JudgementRecord, double?> Value)>();
            foreach (var (id, i18nKey, color, right) in ChartSeries)
            {
                if (selected != null && !selected.Contains(id)) continue;
                active.Add((id, JeI18n.Get(i18nKey), color, right, getters[id]));
            }

            const int w = 1600, h = 900, mL = 90, mR = 110, mT = 40, mB = 56;
            var pw = w - mL - mR;
            var ph = h - mT - mB;
            var finalAcc = Math.Max(0, Math.Min(100, JeaScore.CachedAccuracy / 10000.0));
            var sb = new StringBuilder(96 * 1024);

            sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
                .Append("<title>JustEnoughAcc</title><style>")
                .Append(":root{--bg:#bcc5d6;--card:#ffffff;--ink:#1b2334;--muted:#6a7484;--grid:rgba(22,32,54,.14);--border:rgba(22,32,54,.14);--shadow:rgba(30,40,70,.22);")
                .Append("--s-jeaAcc:#0a9e6b;--s-neaAcc:#3b63d8;--s-jeaScore:#a67c00;--s-neaScore:#8a5cf5;--s-acc:#2a3346;--s-xacc:#b58900;--s-dev:#c65b45;}")
                .Append("@media (prefers-color-scheme:dark){:root{--bg:#0a0e16;--card:#0d1421;--ink:#dfe7f1;--muted:#8a94a6;--grid:rgba(255,255,255,.08);--border:rgba(255,255,255,.10);--shadow:rgba(0,0,0,.55);")
                .Append("--s-jeaAcc:#34d399;--s-neaAcc:#7da2ff;--s-jeaScore:#f3d98b;--s-neaScore:#c9a0ff;--s-acc:#e8f0ff;--s-xacc:#ffd932;--s-dev:#e08a7c;}}")
                .Append("*{box-sizing:border-box}body{background:var(--bg);margin:0;padding:30px 18px;display:flex;justify-content:center;")
                .Append("font-family:system-ui,-apple-system,'Segoe UI',Roboto,'Noto Sans SC',sans-serif;}")
                .Append(".card{background:var(--card);border:1px solid var(--border);border-radius:24px;box-shadow:0 22px 60px var(--shadow);")
                .Append("max-width:1280px;width:100%;padding:30px 34px 26px;-webkit-user-select:none;user-select:none;}")
                .Append("svg{touch-action:none;}")
                .Append("h1{font-size:24px;color:var(--ink);margin:0;font-weight:700;letter-spacing:.01em;}")
                .Append("h1 .brand{color:var(--s-jeaAcc)}")
                .Append(".sub{color:var(--muted);font-size:14px;margin:8px 0 20px;}")
                .Append(".pills{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:16px;}")
                .Append(".pill{display:inline-flex;align-items:center;gap:8px;border:1px solid var(--border);border-radius:999px;padding:5px 13px;font-size:13px;color:var(--ink);cursor:pointer;user-select:none;}")
                .Append(".pill.off{opacity:.35}")
                .Append(".pill .dot{width:16px;height:4px;border-radius:2px;}")
                .Append(".hint{color:var(--muted);font-size:12px;margin:-10px 0 14px;}")
                .Append(".wrap{position:relative;}")
                .Append(".tt{display:none;position:absolute;z-index:5;background:var(--card);border:1px solid var(--border);border-radius:12px;box-shadow:0 8px 26px var(--shadow);padding:10px 14px;font-size:13px;color:var(--ink);pointer-events:none;min-width:180px;}")
                .Append(".tt .th{font-weight:700;margin-bottom:6px;}")
                .Append(".tt .tr{display:flex;align-items:center;gap:8px;margin-top:3px;color:var(--muted);}")
                .Append(".tt .tr b{color:var(--ink);margin-left:auto;}")
                .Append(".tt .td{width:14px;height:4px;border-radius:2px;flex:none;}")
                .Append(".stats{display:flex;gap:14px;margin-top:20px;flex-wrap:wrap;}")
                .Append(".stat{flex:1;min-width:150px;border:1px solid var(--border);border-radius:16px;padding:12px 18px;}")
                .Append(".stat .k{font-size:11px;color:var(--muted);text-transform:uppercase;letter-spacing:.1em;font-weight:600;}")
                .Append(".stat .v{font-size:24px;font-weight:700;color:var(--ink);margin-top:3px;}")
                .Append(".stat .v em{font-style:normal;color:var(--s-jeaAcc);}")
                .Append("svg text{font-family:inherit;}")
                .Append("@media (max-width:720px){body{padding:10px 6px}.card{padding:16px 14px;border-radius:16px}h1{font-size:18px}")
                .Append(".stat{min-width:calc(50% - 8px);padding:10px 14px}.stat .v{font-size:19px}.tt{min-width:150px;font-size:12px}}")
                .Append("</style></head><body><div class=\"card\">");

            sb.Append("<h1><span class=\"brand\">JustEnoughAcc</span> · ").Append(HtmlEscape(levelName)).Append("</h1>");
            sb.Append("<div class=\"sub\">")
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                .Append(" · ").Append(n.ToString(CultureInfo.InvariantCulture)).Append(" tiles</div>");

            if (n == 0 || active.Count == 0)
            {
                sb.Append("<p style=\"color:var(--muted)\">")
                    .Append(n == 0 ? "No records." : "No series selected.").Append("</p></div></body></html>");
                return sb.ToString();
            }

            // legend pills (click to toggle a series on/off)
            sb.Append("<div class=\"pills\">");
            foreach (var s in active)
                AppendPill(sb, s.Id, s.Label);
            sb.Append("</div>");
            sb.Append("<div class=\"hint\">").Append(HtmlEscape(JeI18n.Get("chart.legendHint"))).Append("</div>");

            sb.Append("<div class=\"wrap\" id=\"wrap\">")
                .Append($"<svg id=\"chart\" viewBox=\"0 0 {w} {h}\" style=\"width:100%;display:block;\">");

            // gradient fill under the hero line (JEA Acc)
            var heroOn = active.Exists(s => s.Id == "jeaAcc");
            if (heroOn)
                sb.Append("<defs><linearGradient id=\"areaJeaAcc\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\">")
                    .Append("<stop offset=\"0\" stop-color=\"var(--s-jeaAcc)\" stop-opacity=\"0.30\"/>")
                    .Append("<stop offset=\"1\" stop-color=\"var(--s-jeaAcc)\" stop-opacity=\"0.02\"/>")
                    .Append("</linearGradient></defs>");

            // horizontal grid (0-100), dashed
            for (var v = 0; v <= 100; v += 25)
            {
                var y = YLeft(v, mT, ph);
                sb.Append($"<line x1=\"{mL}\" y1=\"{y:F1}\" x2=\"{w - mR}\" y2=\"{y:F1}\" stroke=\"var(--grid)\" stroke-width=\"1\" stroke-dasharray=\"4 7\"/>");
                sb.Append($"<text x=\"{mL - 12}\" y=\"{y + 4:F1}\" fill=\"var(--muted)\" font-size=\"15\" text-anchor=\"end\">{v}</text>");
            }

            // right axis: deviation in ms, symmetric around 0 (early below, late above)
            var maxDev = NiceCeil(records);
            for (var i = 0; i <= 4; i++)
            {
                var dev = maxDev * (i - 2) / 2.0;
                var y = YRight(dev, maxDev, mT, ph);
                var label = Math.Abs(dev) < 0.05 ? "0" : dev.ToString("0.#", CultureInfo.InvariantCulture);
                sb.Append($"<text x=\"{w - mR + 12}\" y=\"{y + 4:F1}\" fill=\"var(--s-dev)\" font-size=\"15\" opacity=\"0.85\">{label}</text>");
            }

            // x ticks: small marks + tile numbers (regenerated by JS when zooming)
            var step = Math.Max(1, (n + 9) / 10);
            sb.Append("<g id=\"xt\">");
            for (var i = 0; i < n; i += step)
            {
                var x = XOf(i, n, mL, pw);
                sb.Append($"<line x1=\"{x:F1}\" y1=\"{mT + ph}\" x2=\"{x:F1}\" y2=\"{mT + ph + 7}\" stroke=\"var(--grid)\" stroke-width=\"1\"/>");
                sb.Append($"<text x=\"{x:F1}\" y=\"{mT + ph + 28}\" fill=\"var(--muted)\" font-size=\"15\" text-anchor=\"middle\">#{records[i].Tile}</text>");
            }
            sb.Append("</g>");

            // area under JEA Acc (drawn first so lines sit on top)
            if (heroOn)
            {
                var y0 = YLeft(0, mT, ph);
                var segStart = -1;
                // static area render; hidden once JS takes over rendering on zoom/pan
                sb.Append("<g id=\"areastatic\">");
                void FlushArea(int endExclusive)
                {
                    if (segStart < 0) return;
                    sb.Append("<path fill=\"url(#areaJeaAcc)\" d=\"M");
                    for (var i = segStart; i < endExclusive; i++)
                        sb.Append(XOf(i, n, mL, pw).ToString("F1", CultureInfo.InvariantCulture)).Append(',')
                            .Append(YLeft(getters["jeaAcc"](records[i])!.Value, mT, ph).ToString("F1", CultureInfo.InvariantCulture)).Append(' ');
                    sb.Append(XOf(endExclusive - 1, n, mL, pw).ToString("F1", CultureInfo.InvariantCulture)).Append(',').Append(y0.ToString("F1"))
                        .Append(' ').Append(XOf(segStart, n, mL, pw).ToString("F1", CultureInfo.InvariantCulture)).Append(',').Append(y0.ToString("F1"))
                        .Append(" Z\"/>");
                    segStart = -1;
                }
                for (var i = 0; i < n; i++)
                {
                    if (getters["jeaAcc"](records[i]).HasValue)
                    {
                        if (segStart < 0) segStart = i;
                    }
                    else
                    {
                        FlushArea(i);
                    }
                }
                FlushArea(n);
                sb.Append("</g>");

                // final-accuracy reference line
                var yF = YLeft(finalAcc, mT, ph);
                sb.Append($"<line x1=\"{mL}\" y1=\"{yF:F1}\" x2=\"{w - mR}\" y2=\"{yF:F1}\" stroke=\"var(--s-jeaAcc)\" stroke-width=\"1.5\" stroke-dasharray=\"7 7\" opacity=\"0.55\"/>")
                    .Append($"<text x=\"{w - mR - 8}\" y=\"{yF - 8:F1}\" fill=\"var(--s-jeaAcc)\" font-size=\"15\" text-anchor=\"end\" opacity=\"0.9\">")
                    .Append(HtmlEscape(JeI18n.Get("chart.finalAcc"))).Append(' ')
                    .Append(finalAcc.ToString("0.00", CultureInfo.InvariantCulture)).Append("%</text>");
            }

            // series lines
            foreach (var s in active)
                AppendPolyline(sb, records, mL, pw, mT, ph, s.Id, s.Value, "var(--s-" + s.Id + ")", s.Right, maxDev);

            // crosshair + per-series hover dots (driven by the script below)
            sb.Append("<line id=\"xh\" y1=\"").Append(mT).Append("\" y2=\"").Append(mT + ph)
                .Append("\" stroke=\"var(--muted)\" stroke-width=\"1\" stroke-dasharray=\"3 5\" visibility=\"hidden\"/>");
            foreach (var s in active)
                sb.Append("<circle class=\"hdot\" data-id=\"").Append(s.Id).Append("\" r=\"5\" fill=\"var(--s-")
                    .Append(s.Id).Append(")\" stroke=\"var(--card)\" stroke-width=\"2\" visibility=\"hidden\"/>");

            sb.Append("</svg>");
            sb.Append("<div class=\"tt\" id=\"tt\"></div>");
            sb.Append("</div>");

            // footer stats
            sb.Append("<div class=\"stats\">");
            AppendStat(sb, JeI18n.Get("chart.statScore"),
                Math.Floor(JeaScore.TotalScore).ToString(CultureInfo.InvariantCulture));
            AppendStat(sb, JeI18n.Get("chart.statAcc"),
                finalAcc.ToString("0.0000", CultureInfo.InvariantCulture) + "%", true);
            AppendStat(sb, JeI18n.Get("chart.statCombo"),
                "x" + JeaScore.MaxCombo.ToString(CultureInfo.InvariantCulture));
            AppendStat(sb, JeI18n.Get("chart.statTiles"),
                n.ToString(CultureInfo.InvariantCulture));
            sb.Append("</div>");

            AppendChartScript(sb, records, active, mL, pw, mT, ph, w - mR, maxDev);

            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        /// <summary>Embedded data + vanilla-JS interactivity: crosshair tooltip and legend toggles.</summary>
        private static void AppendChartScript(
            StringBuilder sb, IReadOnlyList<JudgementRecord> records,
            List<(string Id, string Label, string Color, bool Right, Func<JudgementRecord, double?> Value)> active,
            int mL, int pw, int mT, int ph, int plotRight, double maxDev)
        {
            var n = records.Count;

            // ---- data (compact JSON; null = gap) ----
            sb.Append("<script>const CFG={n:").Append(n.ToString(CultureInfo.InvariantCulture))
                .Append(",mL:").Append(mL.ToString(CultureInfo.InvariantCulture))
                .Append(",pw:").Append(pw.ToString(CultureInfo.InvariantCulture))
                .Append(",mT:").Append(mT.ToString(CultureInfo.InvariantCulture))
                .Append(",ph:").Append(ph.ToString(CultureInfo.InvariantCulture))
                .Append(",pr:").Append(plotRight.ToString(CultureInfo.InvariantCulture))
                .Append(",md:").Append(maxDev.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(",ra:[").Append(string.Join(",", active.Where(s => s.Right).Select(s => "'" + s.Id + "'")))
                .Append("]};")
                .Append("const L={");
            for (var li = 0; li < active.Count; li++)
                sb.Append(li == 0 ? "" : ",").Append(active[li].Id).Append(":'")
                    .Append(active[li].Label.Replace("'", "\\'")).Append("'");
            sb.Append("};")
                .Append("const D={t:[");
            for (var i = 0; i < n; i++)
                sb.Append(i == 0 ? "" : ",").Append(records[i].Tile.ToString(CultureInfo.InvariantCulture));
            sb.Append("],m:[");
            for (var i = 0; i < n; i++)
                sb.Append(i == 0 ? "" : ",").Append('"').Append(MarginLabel(records[i].Margin)).Append('"');
            sb.Append("],ts:[");
            for (var i = 0; i < n; i++)
                sb.Append(i == 0 ? "" : ",").Append('"').Append(FormatTimestamp(records[i].Timestamp)).Append('"');
            sb.Append("]");
            foreach (var s in active)
            {
                sb.Append(",").Append(s.Id).Append(":[");
                for (var i = 0; i < n; i++)
                {
                    if (i > 0) sb.Append(',');
                    var v = s.Value(records[i]);
                    if (v.HasValue)
                        sb.Append(v.Value.ToString("0.###", CultureInfo.InvariantCulture));
                    else
                        sb.Append("null");
                }
                sb.Append("]");
            }
            sb.Append("};");

            // ---- interactivity ----
            sb.Append(@"
const svg=document.getElementById('chart'),wrap=document.getElementById('wrap'),tt=document.getElementById('tt'),xh=document.getElementById('xh');
const hdots={};document.querySelectorAll('.hdot').forEach(c=>hdots[c.dataset.id]=c);
const seriesEls={};document.querySelectorAll('.series').forEach(g=>{(seriesEls[g.dataset.id]=seriesEls[g.dataset.id]||[]).push(g)});
const pillEls={};document.querySelectorAll('.pill').forEach(p=>pillEls[p.dataset.series]=p);
const NS=Math.max(1,CFG.n-1);
let z0=0,z1=NS,managed=false,drag=null;
function clamp(v,a,b){return v<a?a:v>b?b:v}
function span(){return z1-z0}
function vX(i){return CFG.mL+(i-z0)/Math.max(1e-6,span())*CFG.pw}
function yOf(id,v){const right=CFG.ra.indexOf(id)>=0;if(right){const m=CFG.md;return CFG.mT+CFG.ph*(1-(clamp(v,-m,m)+m)/(2*m))}return CFG.mT+CFG.ph*(1-clamp(v,0,100)/100)}
function hidden(id){const gs=seriesEls[id];return !gs||gs.length===0||gs.every(g=>g.style.display==='none')}
Object.keys(pillEls).forEach(id=>{const el=pillEls[id];
 const t=()=>{const off=!hidden(id);(seriesEls[id]||[]).forEach(g=>g.style.display=off?'none':'');el.classList.toggle('off',off);
  if(hdots[id])hdots[id].setAttribute('visibility','hidden');};
 el.addEventListener('click',t);
 el.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();t()}});});
function rebuild(){
 if(!managed){managed=true;const a=document.getElementById('areastatic');if(a)a.style.display='none';}
 for(const id of Object.keys(D).slice(3)){
  const gs=seriesEls[id];if(!gs||!gs.length)continue;
  const arr=D[id];
  const i0=Math.max(0,Math.floor(z0)),i1=Math.min(CFG.n-1,Math.ceil(z1));
  let cnt=0;for(let i=i0;i<=i1;i++)if(arr[i]!=null)cnt++;
  const dots=cnt<=300;
  let d='',circ='',open=false,first=-1,last=-1;
  for(let i=i0;i<=i1;i++){
   const v=arr[i];if(v==null){open=false;continue}
   const x=vX(i).toFixed(1),y=yOf(id,+v).toFixed(1);
   if(!open){d+='M'+x+','+y;open=true}else d+='L'+x+','+y;
   if(first<0)first=i;last=i;
   if(dots)circ+='<circle cx=\''+x+'\' cy=\''+y+'\' r=\'3\' fill=\'var(--s-'+id+')\'/>';
  }
  let inner='';
  if(id==='jeaAcc'&&first>=0){
   const yb=(CFG.mT+CFG.ph).toFixed(1);
   inner+='<path fill=\'url(#areaJeaAcc)\' d=\''+d+'L'+vX(last).toFixed(1)+','+yb+'L'+vX(first).toFixed(1)+','+yb+'Z\'/>';
  }
  inner+='<path fill=\'none\' stroke=\'var(--s-'+id+')\' stroke-width=\'2.5\' stroke-linejoin=\'round\' d=\''+d+'\'/>'+circ;
  gs.forEach(g=>g.innerHTML=inner);
 }
 const s=Math.max(1,Math.round(span()/9));let h='';
 for(let i=Math.ceil(z0/s)*s;i<=z1&&i<CFG.n;i+=s){
  const x=vX(i).toFixed(1);
  h+='<line x1=\''+x+'\' y1=\''+(CFG.mT+CFG.ph)+'\' x2=\''+x+'\' y2=\''+(CFG.mT+CFG.ph+7)+'\' stroke=\'var(--grid)\' stroke-width=\'1\'/><text x=\''+x+'\' y=\''+(CFG.mT+CFG.ph+28)+'\' fill=\'var(--muted)\' font-size=\'15\' text-anchor=\'middle\'>#'+D.t[i]+'</text>';
 }
 document.getElementById('xt').innerHTML=h;
}
function show(e){
 const r=svg.getBoundingClientRect(),sx=1600/r.width;
 const x=(e.clientX-r.left)*sx;
 if(x<CFG.mL||x>CFG.pr){hide();return}
 const i=CFG.n===1?0:clamp(Math.round(z0+(x-CFG.mL)/CFG.pw*span()),0,CFG.n-1);
 const px=vX(i);
 xh.setAttribute('x1',px);xh.setAttribute('x2',px);xh.setAttribute('visibility','visible');
 let rows='';
 for(const id of Object.keys(D).slice(3)){
  if(hidden(id))continue;
  const v=D[id][i];
  if(v==null)continue;
  rows+='<div class=\'tr\'><span class=\'td\' style=\'background:var(--s-'+id+')\'></span>'+(L[id]||id)+' <b>'+(+v).toFixed(2)+(id===\'dev\'?'ms':(CFG.ra.indexOf(id)>=0?'°':''))+'</b></div>';
 }
 tt.innerHTML='<div class=\'th\'>#'+D.t[i]+' · '+D.m[i]+' · '+D.ts[i]+'</div>'+rows;
 tt.style.display='block';
 const wr=wrap.getBoundingClientRect();
 let lx=e.clientX-wr.left+16,ly=e.clientY-wr.top-10;
 if(lx+tt.offsetWidth>wr.width-8)lx=e.clientX-wr.left-tt.offsetWidth-16;
 if(ly+tt.offsetHeight>wr.height-8)ly=wr.height-tt.offsetHeight-8;
 if(ly<8)ly=8;
 tt.style.left=lx+'px';tt.style.top=ly+'px';
 for(const id of Object.keys(hdots)){
  const v=(D[id]&&!hidden(id))?D[id][i]:null;
  if(v==null){hdots[id].setAttribute('visibility','hidden');continue}
  hdots[id].setAttribute('cx',px);hdots[id].setAttribute('cy',yOf(id,v));hdots[id].setAttribute('visibility','visible');
 }
}
function hide(){xh.setAttribute('visibility','hidden');tt.style.display='none';Object.values(hdots).forEach(c=>c.setAttribute('visibility','hidden'))}
svg.style.cursor='crosshair';
svg.addEventListener('pointerdown',e=>{e.preventDefault();drag={x:e.clientX,a:z0,b:z1};svg.setPointerCapture(e.pointerId);svg.style.cursor='grabbing'});
svg.addEventListener('pointerup',()=>{drag=null;svg.style.cursor='crosshair'});
svg.addEventListener('pointermove',e=>{
 if(drag){
  const r=svg.getBoundingClientRect();
  const di=(drag.x-e.clientX)/r.width*1600/CFG.pw*span();
  const sp=drag.b-drag.a;let nz0=drag.a+di,nz1=drag.b+di;
  if(nz0<0){nz0=0;nz1=sp}
  if(nz1>NS){nz1=NS;nz0=NS-sp}
  z0=nz0;z1=nz1;hide();rebuild();return;
 }
 show(e);
});
svg.addEventListener('pointerleave',hide);
svg.addEventListener('wheel',e=>{
 e.preventDefault();
 if(CFG.n<2)return;
 const r=svg.getBoundingClientRect(),sx=1600/r.width;
 const x=clamp((e.clientX-r.left)*sx,CFG.mL,CFG.pr);
 const f=(x-CFG.mL)/CFG.pw;
 const anchor=z0+f*span();
 const k=e.deltaY>0?1.25:0.8;
 const sp=clamp(span()*k,1,NS);
 z0=clamp(anchor-f*sp,0,NS-sp);z1=z0+sp;
 rebuild();hide();
},{passive:false});
svg.addEventListener('dblclick',()=>{z0=0;z1=NS;rebuild()});
</script>");
        }

        private static void AppendPill(StringBuilder sb, string id, string label)
        {
            sb.Append("<span class=\"pill\" data-series=\"").Append(id).Append("\" role=\"button\" tabindex=\"0\">")
                .Append("<span class=\"dot\" style=\"background:var(--s-").Append(id).Append(")\"></span>")
                .Append(HtmlEscape(label)).Append("</span>");
        }

        private static void AppendStat(StringBuilder sb, string key, string value, bool accent = false)
        {
            sb.Append("<div class=\"stat\"><div class=\"k\">").Append(HtmlEscape(key)).Append("</div><div class=\"v\">");
            if (accent) sb.Append("<em>").Append(HtmlEscape(value)).Append("</em>");
            else sb.Append(HtmlEscape(value));
            sb.Append("</div></div>");
        }

        private static Dictionary<string, Func<JudgementRecord, double?>> ChartGetters()
        {
            return new Dictionary<string, Func<JudgementRecord, double?>>
            {
                // JEA accuracy is stored in ten-thousandths of a percent (10000 == 1%)
                ["jeaAcc"] = r => r.JeaAccuracy / 10000.0,
                ["neaAcc"] = r => r.NeaAcc,
                ["jeaScore"] = r => r.JeaTileScore,
                // NEA per-tile: null or -1 (noop) is a gap; miss/overload codes clamp to 0
                ["neaScore"] = r => r.NeaScore is < 0 ? (r.NeaScore == -1 ? null : 0.0) : r.NeaScore,
                ["acc"] = r => r.Acc * 100.0,
                ["xacc"] = r => r.XAcc * 100.0,
                // Signed ms deviation (positive = late, negative = early); the
                // right axis is symmetric around 0 so direction is visible.
                ["dev"] = r => r.NormalizedDeviationDeg,
            };
        }

        private static double NiceCeil(IReadOnlyList<JudgementRecord> records)
        {
            var max = 0.0;
            foreach (var r in records)
            {
                var d = Math.Abs(r.NormalizedDeviationDeg);
                if (d > max) max = d;
            }
            if (double.IsNaN(max) || double.IsInfinity(max) || max <= 0) return 5;
            foreach (var step in new[] { 1.0, 2, 5, 10, 20, 50, 100, 200 })
            {
                if (max <= step) return step;
            }
            return Math.Ceiling(max / 200.0) * 200.0;
        }

        private static float XOf(int i, int n, int mL, int pw)
        {
            return (float)(mL + (n == 1 ? 0.5 : (double)i / (n - 1)) * pw);
        }

        private static float YLeft(double v0100, int mT, int ph)
        {
            return (float)(mT + ph * (1.0 - Math.Max(0, Math.Min(100, v0100)) / 100.0));
        }

        private static float YRight(double dev, double maxDev, int mT, int ph)
        {
            if (maxDev <= 0) maxDev = 1;
            // Symmetric around 0: -maxDev at the bottom, +maxDev at the top,
            // 0 in the middle (early below, late above).
            var clamped = Math.Max(-maxDev, Math.Min(maxDev, dev));
            return (float)(mT + ph * (1.0 - (clamped + maxDev) / (2.0 * maxDev)));
        }

        /// <summary>
        /// Draw one series as polyline segments (null values break the line) inside
        /// a &lt;g class="series"&gt; wrapper the page script can address. Hover info
        /// is provided by the JS crosshair tooltip, not native titles.
        /// </summary>
        private static void AppendPolyline(
            StringBuilder sb, IReadOnlyList<JudgementRecord> records,
            int mL, int pw, int mT, int ph, string id,
            Func<JudgementRecord, double?> value, string cssColor, bool rightAxis, double maxDev)
        {
            var n = records.Count;
            sb.Append("<g class=\"series\" data-id=\"").Append(id).Append("\">");

            // collect contiguous segments of non-null values
            var segStart = -1;
            void FlushSegment(int endExclusive)
            {
                if (segStart < 0) return;
                sb.Append("<path fill=\"none\" stroke=\"").Append(cssColor)
                    .Append("\" stroke-width=\"2.5\" stroke-linejoin=\"round\" d=\"M");
                for (var i = segStart; i < endExclusive; i++)
                {
                    var v = value(records[i])!.Value;
                    sb.Append(XOf(i, n, mL, pw).ToString("F1", CultureInfo.InvariantCulture)).Append(',')
                        .Append((rightAxis ? YRight(v, maxDev, mT, ph) : YLeft(v, mT, ph))
                            .ToString("F1", CultureInfo.InvariantCulture)).Append(' ');
                }
                sb.Append("\"/>");
                segStart = -1;
            }

            for (var i = 0; i < n; i++)
            {
                if (value(records[i]).HasValue)
                {
                    if (segStart < 0) segStart = i;
                }
                else
                {
                    FlushSegment(i);
                }
            }
            FlushSegment(n);

            // static point markers for short runs (hover detail comes from the JS tooltip)
            var nonNull = 0;
            for (var i = 0; i < n; i++)
                if (value(records[i]).HasValue) nonNull++;
            if (nonNull <= 300)
            {
                for (var i = 0; i < n; i++)
                {
                    var vOpt = value(records[i]);
                    if (!vOpt.HasValue) continue;
                    var v = vOpt.Value;
                    sb.Append("<circle cx=\"").Append(XOf(i, n, mL, pw).ToString("F1", CultureInfo.InvariantCulture))
                        .Append("\" cy=\"").Append((rightAxis ? YRight(v, maxDev, mT, ph) : YLeft(v, mT, ph))
                            .ToString("F1", CultureInfo.InvariantCulture))
                        .Append("\" r=\"3\" fill=\"").Append(cssColor).Append("\"/>");
                }
            }

            sb.Append("</g>");
        }

        /// <summary>Localized HitMargin name; falls back to the enum name when no i18n entry exists.
        /// The 3.4.0 perfect trio collapses to the classic "Perfect" name for stable exports.</summary>
        private static string MarginLabel(HitMargin margin)
        {
            var name = HitMarginCompat.DisplayName(margin);
            var key = "margin." + name;
            var s = JeI18n.Get(key);
            return s == key ? name : s;
        }

        private static string HtmlEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        private static string Escape(string value)
        {
            if (value == null) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static string FormatTimestamp(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) seconds = 0;
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return $"{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds:000}";
        }

        /// <summary>Format chart seconds as mm:ss.mmm (shared with the previewer UI).</summary>
        public static string FormatTimestampPublic(double seconds)
        {
            return FormatTimestamp(seconds);
        }

        private static string FormatNum(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatPct(float value)
        {
            return (value * 100f).ToString("0.00", CultureInfo.InvariantCulture) + "%";
        }

        private static string FormatAccuracy(long acc)
        {
            return (acc / 10000m).ToString("0.0000", CultureInfo.InvariantCulture) + "%";
        }
    }
}
