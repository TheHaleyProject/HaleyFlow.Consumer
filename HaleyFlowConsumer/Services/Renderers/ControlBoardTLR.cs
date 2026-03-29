using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Haley.Enums;
using Haley.Models;

namespace Haley.Services;

/// <summary>
/// Converts a <see cref="ConsumerTimeline"/> into a self-contained HTML page.
/// </summary>
internal static class ControlBoardTLR {

    public static string Render(ConsumerTimeline timeline, string? consumerGuid = null, string? displayName = null, string? color = null) {
        if (timeline == null) return string.Empty;

        var items = timeline.Items ?? Array.Empty<ConsumerTimelineItem>();
        var pageTitle = !string.IsNullOrWhiteSpace(timeline.InstanceGuid)
            ? $"CTL-{timeline.InstanceGuid}"
            : "CTL-UNKNOWN";

        var sb = new StringBuilder(32_000);
        WriteHead(sb, pageTitle, color);
        sb.Append("<div class=\"shell\">\n");
        WriteRail(sb, timeline, items, consumerGuid);
        WriteMain(sb, timeline, items, displayName);
        sb.Append("</div>\n</body>\n</html>\n");
        return sb.ToString();
    }

    // ── Head ─────────────────────────────────────────────────────────────────

    private static void WriteHead(StringBuilder sb, string pageTitle, string? color) {
        sb.Append("""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
""");
        sb.Append($"  <title>{E(pageTitle)}</title>\n");
        sb.Append("""
  <style>
    :root {
      --bg-a: #f8f4ec; --bg-b: #ece2d4;
      --card: rgba(255,251,245,.92); --card-strong: #fffdf9;
      --ink: #1d2830; --muted: #68747d; --line: rgba(23,37,44,.12);
      --teal: #0f6a70; --amber: #9d6b17; --red: #b24a34; --green: #2f714e;
      --shadow: 0 24px 70px rgba(23,30,26,.12); --radius-xl: 30px;
      --mono: Consolas, monospace;
      --ui: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
    }
    *, *::before, *::after { box-sizing: border-box; min-width: 0; }
    html, body { margin: 0; min-height: 100%; }
    body {
      font-family: var(--ui); color: var(--ink);
      background:
        radial-gradient(circle at top left, rgba(15,106,112,.15), transparent 30%),
        radial-gradient(circle at top right, rgba(178,74,52,.14), transparent 28%),
        linear-gradient(180deg, var(--bg-a) 0%, var(--bg-b) 100%);
    }
    body::before {
      content: ""; position: fixed; inset: 0; pointer-events: none;
      background-image:
        linear-gradient(rgba(29,40,48,.028) 1px, transparent 1px),
        linear-gradient(90deg, rgba(29,40,48,.028) 1px, transparent 1px);
      background-size: 22px 22px;
      mask-image: radial-gradient(circle at center, black 35%, transparent 95%);
    }
    .shell {
      position: relative; z-index: 1; max-width: 1680px; margin: 0 auto; padding: 26px;
      display: grid; grid-template-columns: 392px minmax(0,1fr); gap: 26px; align-items: start;
    }
    .rail {
      position: sticky; top: 18px; align-self: start; max-height: calc(100vh - 36px);
      display: flex; flex-direction: column; gap: 18px;
    }
    .rail-top { flex: 0 0 auto; }
    .rail-scroll {
      min-height: 0; overflow-y: auto; display: flex; flex-direction: column; gap: 18px; padding-right: 4px;
    }
    .main { display: flex; flex-direction: column; gap: 20px; }
    .card, .hero, .event-card {
      background: var(--card); border: 1px solid rgba(255,255,255,.72);
      box-shadow: var(--shadow); backdrop-filter: blur(16px);
    }
    .card { border-radius: var(--radius-xl); padding: 20px; }
    .eyebrow, .section-title, .k-label, .meta-label, .prop-key {
      text-transform: uppercase; letter-spacing: .14em; font-size: .75rem; color: var(--muted);
    }
    .eyebrow { color: var(--teal); font-weight: 700; margin-bottom: 12px; }
    .page-head { padding: 18px 22px; }
    .page-title { margin: 0; font-size: clamp(1.18rem,1.55vw,1.55rem); font-weight: 900; line-height: 1.08; }
    .toolbar-stick { position: sticky; top: 18px; z-index: 6; }
    .chip-row, .toolbar-meta { display: flex; flex-wrap: wrap; gap: 10px; }
    .chip { padding: 8px 12px; border-radius: 999px; border: 1px solid var(--line); background: rgba(255,255,255,.74); font-size: .83rem; }
    .chip strong, .mono { font-family: var(--mono); overflow-wrap: anywhere; word-break: break-word; }
    .stats-grid, .meta-grid, .duo-grid, .summary-grid { display: grid; gap: 12px; }
    .stats-grid { grid-template-columns: repeat(2, minmax(0,1fr)); }
    .stat, .meta-item, .mini-panel { border: 1px solid var(--line); background: rgba(255,255,255,.74); border-radius: 18px; }
    .stat { padding: 14px; }
    .stat strong { display: block; font-size: 1.32rem; margin-bottom: 4px; }
    .heat-stack { display: grid; gap: 10px; }
    .heat-row { border: 1px solid var(--line); background: rgba(255,255,255,.74); border-radius: 16px; padding: 12px; }
    .heat-head { display: flex; justify-content: space-between; gap: 10px; margin-bottom: 8px; align-items: center; }
    .bar { height: 8px; border-radius: 999px; background: rgba(23,37,44,.08); overflow: hidden; }
    .fill { height: 100%; background: linear-gradient(90deg, #d69229, #b24a34); }
    .timeline { display: flex; flex-direction: column; gap: 18px; }
    .event-card { border-radius: 30px; padding: 20px; position: relative; transition: padding .18s ease; }
    .event-card::before {
      content: ""; position: absolute; left: 0; top: 0; bottom: 0; width: 7px;
      background: var(--accent, var(--teal)); border-radius: 30px 0 0 30px;
    }
    .event-head {
      display: grid; grid-template-columns: 64px minmax(0,1fr) auto 22px;
      gap: 16px; align-items: start; margin-bottom: 16px;
      cursor: pointer; user-select: none;
    }
    .event-mark {
      width: 64px; height: 64px; border-radius: 20px; display: grid; place-items: center;
      color: white; font-size: 1.25rem; font-weight: 700;
      background: linear-gradient(160deg, var(--accent, var(--teal)), rgba(22,29,35,.96));
    }
    .event-title { margin: 0 0 6px; font-size: 1.18rem; font-weight: 900; line-height: 1.1; }
    .prop-row { display: flex; flex-wrap: wrap; gap: 6px 14px; margin-top: 4px; }
    .prop { display: inline-flex; align-items: baseline; gap: 5px; }
    .prop-val { font-size: .8rem; font-family: var(--mono); color: var(--ink); }
    .badge-stack { display: flex; flex-direction: column; gap: 8px; align-items: flex-end; }
    .badge {
      display: inline-flex; align-items: center; padding: 8px 12px; border-radius: 999px;
      text-transform: uppercase; letter-spacing: .05em; font-size: .74rem; font-weight: 700; white-space: nowrap;
    }
    .badge.fail  { background: rgba(178,74,52,.13);  color: var(--red);   }
    .badge.warn  { background: rgba(157,107,23,.14); color: var(--amber); }
    .badge.good  { background: rgba(47,113,78,.14);  color: var(--green); }
    .badge.info  { background: rgba(15,106,112,.12); color: var(--teal);  }
    .expand-chevron {
      font-size: .95rem; color: var(--muted); line-height: 1;
      padding-top: 6px; transition: transform .22s ease; align-self: start; text-align: center;
    }
    .event-card.collapsed .expand-chevron { transform: rotate(-90deg); }
    .event-card.collapsed { padding-top: 14px; padding-bottom: 14px; }
    .event-card.collapsed .event-head {
      grid-template-columns: 50px minmax(0,1fr) auto 18px;
      gap: 12px; margin-bottom: 0;
    }
    .event-card.collapsed .event-mark {
      width: 50px; height: 50px; border-radius: 16px; font-size: 1.02rem;
    }
    .event-card.collapsed .section-title { font-size: .68rem; letter-spacing: .12em; }
    .event-card.collapsed .event-title { margin-bottom: 4px; font-size: .98rem; }
    .event-card.collapsed .prop-row { gap: 4px 10px; }
    .event-card.collapsed .prop-key,
    .event-card.collapsed .prop-val { font-size: .72rem; }
    .event-card.collapsed .badge-stack { gap: 6px; }
    .event-card.collapsed .badge { padding: 5px 9px; font-size: .66rem; }
    .event-card.collapsed .expand-chevron { padding-top: 2px; font-size: .82rem; }
    .event-body-wrap { display: grid; grid-template-rows: 1fr; transition: grid-template-rows .28s ease; }
    .event-card.collapsed .event-body-wrap { grid-template-rows: 0fr; }
    .event-body { overflow: hidden; min-height: 0; }
    .meta-grid { grid-template-columns: repeat(3, minmax(0,1fr)); margin-bottom: 18px; }
    .meta-item { padding: 14px 16px; }
    .meta-value { margin-top: 6px; line-height: 1.55; }
    .duo-grid { grid-template-columns: repeat(2, minmax(0,1fr)); margin-bottom: 18px; }
    .section-box { border-radius: 22px; padding: 18px; border: 1px solid var(--line); background: rgba(255,255,255,.76); }
    .summary-grid { grid-template-columns: repeat(2, minmax(0,1fr)); margin-top: 12px; }
    .mini-panel { padding: 12px 14px; }
    .mini-panel.fail { background: rgba(178,74,52,.1);  border-color: rgba(178,74,52,.22); }
    .mini-panel.warn { background: rgba(157,107,23,.09); border-color: rgba(157,107,23,.2); }
    .mini-panel.good { background: rgba(47,113,78,.09);  border-color: rgba(47,113,78,.2); }
    .mini-panel.fail .mini-value { color: var(--red);   font-weight: 700; }
    .mini-panel.warn .mini-value { color: var(--amber); font-weight: 700; }
    .mini-panel.good .mini-value { color: var(--green); font-weight: 700; }
    .mini-value { margin-top: 6px; line-height: 1.5; }
    .scroll-box {
      margin-top: 12px; max-height: 126px; overflow: auto; padding: 12px 14px;
      border-radius: 14px; border: 1px solid rgba(178,74,52,.16); background: rgba(178,74,52,.08);
      color: #8d3928; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere;
    }
    .neutral-box {
      margin-top: 12px; padding: 10px 14px; border-radius: 14px;
      border: 1px solid var(--line); background: rgba(23,37,44,.04); color: var(--muted); font-size: .85rem;
    }
    .table-wrap {
      margin-top: 12px; max-height: 360px; overflow: auto;
      border: 1px solid var(--line); border-radius: 16px; background: var(--card-strong);
    }
    table { width: 100%; border-collapse: collapse; font-size: .9rem; }
    thead th {
      position: sticky; top: 0; z-index: 1; text-align: left; padding: 12px 14px;
      background: #f6eee1; border-bottom: 1px solid var(--line);
      color: var(--muted); font-size: .75rem; letter-spacing: .08em; text-transform: uppercase;
    }
    tbody td { padding: 12px 14px; border-bottom: 1px solid rgba(23,37,44,.08); vertical-align: top; line-height: 1.45; }
    tbody tr:last-child td { border-bottom: 0; }
    .empty { padding: 14px; border-radius: 14px; border: 1px dashed rgba(23,37,44,.14); background: rgba(23,37,44,.04); color: var(--muted); font-size: .85rem; }
    .actions-box { margin-bottom: 18px; }
    .timeline-toolbar {
      display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap;
      margin-bottom: 4px;
    }
    .toolbar-actions { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
    .btn-toolbar {
      padding: 7px 14px; border-radius: 999px; border: 1px solid var(--line);
      background: rgba(255,255,255,.74); font-family: var(--ui); font-size: .8rem;
      cursor: pointer; color: var(--ink); transition: background .15s;
    }
    .btn-toolbar:hover { background: rgba(255,255,255,.95); }
    ::-webkit-scrollbar { width: 10px; height: 10px; }
    ::-webkit-scrollbar-track { background: rgba(23,37,44,.05); border-radius: 999px; }
    ::-webkit-scrollbar-thumb { background: rgba(23,37,44,.22); border-radius: 999px; }
    @media (max-width: 1220px) {
      .shell, .meta-grid, .duo-grid { grid-template-columns: 1fr; }
      .rail { position: static; max-height: none; }
      .rail-scroll { max-height: none; overflow: visible; padding-right: 0; }
      .toolbar-stick { position: static; }
      .badge-stack { align-items: flex-start; }
    }
    @media (max-width: 760px) {
      .shell { padding: 14px; gap: 16px; }
      .page-head, .event-card, .card { border-radius: 22px; }
      .event-head { grid-template-columns: 1fr; }
      .event-mark { width: 58px; height: 58px; border-radius: 18px; }
      .event-card.collapsed .event-head { grid-template-columns: 1fr; }
      .stats-grid, .summary-grid { grid-template-columns: 1fr; }
    }
  </style>
  <script>
    function toggleConsumerCard(head) {
      head.closest('.event-card').classList.toggle('collapsed');
    }
    function collapseAll() {
      document.querySelectorAll('.event-card').forEach(function(c) { c.classList.add('collapsed'); });
    }
    function expandAll() {
      document.querySelectorAll('.event-card').forEach(function(c) { c.classList.remove('collapsed'); });
    }
  </script>
</head>
<body>
""");

        if (!string.IsNullOrWhiteSpace(color))
            sb.Append($"<style>:root {{ --teal: {E(color.Trim())}; }}</style>\n");
    }

    // ── Rail ─────────────────────────────────────────────────────────────────

    private static void WriteRail(StringBuilder sb, ConsumerTimeline timeline, IReadOnlyList<ConsumerTimelineItem> items, string? consumerGuid) {
        var failedCount   = CountWhere(items, i => i.InboxStatus?.Status == "Failed");
        var hookCount     = CountWhere(items, i => i.Kind == "Hook");
        var transCount    = CountWhere(items, i => i.Kind == "Transition");
        var totalAttempts = SumInt(items, i => i.InboxStatus?.AttemptCount ?? 0);
        var totalHistory  = SumInt(items, i => i.Outbox?.History?.Count ?? 0);
        var maxAttempts   = MaxInt(items, i => i.InboxStatus?.AttemptCount ?? 0);

        sb.Append($"""
  <aside class="rail">
    <div class="rail-top">
      <section class="card">
        <div class="section-title">Instance</div>
        <div class="mono" style="overflow-wrap:anywhere;font-size:.85rem;margin-top:6px">{E(timeline.InstanceGuid)}</div>
        <div style="display:flex;flex-direction:column;gap:6px;margin-top:10px">
""");

        if (!string.IsNullOrWhiteSpace(consumerGuid))
            sb.Append($"        {RailIdRow("consumer", consumerGuid)}\n");

        if (!string.IsNullOrWhiteSpace(timeline.EntityGuid))
            sb.Append($"        {RailIdRow("entity", timeline.EntityGuid)}\n");

        if (!string.IsNullOrWhiteSpace(timeline.DefName))
            sb.Append($"        {RailIdRow("definition", timeline.DefName)}\n");

        sb.Append($"        {RailIdRow("records", items.Count.ToString())}\n");
        sb.Append("""
        </div>
      </section>
    </div>
    <div class="rail-scroll">
""");

        sb.Append($"""
      <section class="card">
        <div class="section-title">Summary</div>
        <div class="stats-grid">
          {Stat(items.Count.ToString(),     "records")}
          {Stat(failedCount.ToString(),     "failed")}
          {Stat(totalAttempts.ToString(),   "attempts")}
          {Stat(totalHistory.ToString(),    "history")}
          {Stat(hookCount.ToString(),       "hooks")}
          {Stat(transCount.ToString(),      "transitions")}
        </div>
      </section>
      <section class="card">
        <div class="section-title">Trigger Heat</div>
        <div class="heat-stack">
""");

        for (var i = 0; i < items.Count; i++) {
            var item     = items[i];
            var attempts = item.InboxStatus?.AttemptCount ?? 0;
            var width    = maxAttempts > 0 ? Math.Max(12, (int)Math.Round((double)attempts / maxAttempts * 100)) : 12;
            var hookTag  = item.HookType == HookType.Effect ? "[E] " : (item.Kind == "Hook" ? "[G] " : string.Empty);
            var label    = item.Kind == "Hook"
                ? hookTag + (item.Route ?? "hook")
                : $"event_code: {item.EventCode?.ToString() ?? "\u2014"}";
            sb.Append($"""
        <div class="heat-row">
          <div class="heat-head">
            <span class="mono" style="font-size:.76rem;color:var(--muted)">{E(label)}</span>
            <strong>{attempts}</strong>
          </div>
          <div class="bar"><div class="fill" style="width:{width}%"></div></div>
        </div>
""");
        }

        sb.Append("""
        </div>
      </section>
    </div>
  </aside>
""");
    }

    // ── Main ─────────────────────────────────────────────────────────────────

    private static void WriteMain(StringBuilder sb, ConsumerTimeline timeline, IReadOnlyList<ConsumerTimelineItem> items, string? displayName) {
        var title = !string.IsNullOrWhiteSpace(displayName) ? displayName : timeline.InstanceGuid;
        var versionChip = timeline.DefVersion > 0
            ? $"<div class=\"chip\">version <strong>{timeline.DefVersion}</strong></div>"
            : string.Empty;

        sb.Append($"""
  <main class="main">
    <section class="card page-head">
      <h1 class="page-title">{E(title)}</h1>
    </section>

    <div class="toolbar-stick">
      <div class="card timeline-toolbar">
        <div class="toolbar-meta">
          <div class="chip">definition <strong>{E(timeline.DefName ?? "\u2014")}</strong></div>
          {versionChip}
          <div class="chip">entity <strong class="mono">{E(timeline.EntityGuid ?? "\u2014")}</strong></div>
          <div class="chip">records <strong>{items.Count}</strong></div>
        </div>
        <div class="toolbar-actions">
          <button class="btn-toolbar" onclick="collapseAll()">Collapse All</button>
          <button class="btn-toolbar" onclick="expandAll()">Expand All</button>
        </div>
      </div>
    </div>

    <section class="timeline">
""");

        // InboxId is DB auto-increment — insertion order reflects actual arrival sequence (transition first, then hooks).
        var sorted = items.OrderBy(x => x.InboxId).ToList();
        for (var i = 0; i < sorted.Count; i++)
            WriteItem(sb, sorted[i]);

        sb.Append("""
    </section>
  </main>
""");
    }

    // ── Event card ───────────────────────────────────────────────────────────

    private static void WriteItem(StringBuilder sb, ConsumerTimelineItem item) {
        var isHook     = item.Kind == "Hook";
        var isGate     = isHook && item.HookType != HookType.Effect;  // Gate=1 (default when null), Effect=0
        var accent     = isHook ? (isGate ? "#9d6b17" : "#2f714e") : "#0f6a70";
        var mark       = isHook ? (isGate ? "G" : "E") : "T";
        var eventTitle = isHook
            ? (item.Route ?? "hook")
            : $"event_code: {item.EventCode?.ToString() ?? "\u2014"}";

        var inboxTone   = Tone(item.InboxStatus?.Status);
        var outboxBadge = $"{item.Outbox?.Outcome ?? "\u2014"} / {item.Outbox?.Status ?? "\u2014"}";
        var outboxTone  = Tone(item.Outbox?.Outcome);

        sb.Append($"""
      <article class="event-card" style="--accent:{accent}">
        <div class="event-head" onclick="toggleConsumerCard(this)">
          <div class="event-mark">{E(mark)}</div>
          <div>
            <div class="section-title">{E(item.Kind)}</div>
            <h2 class="event-title">{E(eventTitle)}</h2>
            <div class="prop-row">
              {Prop("inbox_id", item.InboxId.ToString())}
              {Prop("occurred", FmtFull(item.Occurred))}
              {Prop("created",  FmtFull(item.Created))}
            </div>
          </div>
          <div class="badge-stack">
            <div class="badge {inboxTone}">{E(item.InboxStatus?.Status ?? "\u2014")}</div>
            <div class="badge {outboxTone}">{E(outboxBadge)}</div>
          </div>
          <div class="expand-chevron">&#9660;</div>
        </div>
        <div class="event-body-wrap"><div class="event-body">
          <div class="meta-grid">
            <div class="meta-item">
              <div class="meta-label">ack_guid</div>
              <div class="meta-value mono">{E(item.AckGuid)}</div>
            </div>
            <div class="meta-item">
              <div class="meta-label">handler_version</div>
              <div class="meta-value">{E(item.HandlerVersion?.ToString() ?? "\u2014")}</div>
            </div>
            <div class="meta-item">
              <div class="meta-label">run_count</div>
              <div class="meta-value">{E(item.RunCount.ToString())}</div>
            </div>
          </div>
          <div class="duo-grid">
""");

        WriteInboxStatusPanel(sb, item.InboxStatus);
        WriteOutboxPanel(sb, item.Outbox);

        sb.Append("""
          </div>
""");

        WriteActionsSection(sb, item.Actions);
        WriteOutboxHistorySection(sb, item.Outbox?.History);

        sb.Append("""
        </div></div>
      </article>
""");
    }

    // ── Inbox status panel ───────────────────────────────────────────────────

    private static void WriteInboxStatusPanel(StringBuilder sb, ConsumerTimelineStatus? s) {
        var st         = s?.Status       ?? "\u2014";
        var attempts   = s?.AttemptCount.ToString() ?? "\u2014";
        var receivedAt = s != null ? FmtFull(s.ReceivedAt) : "\u2014";
        var modified   = s != null ? FmtFull(s.Modified)   : "\u2014";
        var hasError   = !string.IsNullOrWhiteSpace(s?.LastError);

        sb.Append($"""
            <section class="section-box">
              <div class="section-title">inbox_status</div>
              <div class="summary-grid">
                <div class="mini-panel {Tone(s?.Status)}"><div class="meta-label">status</div><div class="mini-value">{E(st)}</div></div>
                <div class="mini-panel"><div class="meta-label">attempt_count</div><div class="mini-value">{E(attempts)}</div></div>
                <div class="mini-panel"><div class="meta-label">received_at</div><div class="mini-value">{E(receivedAt)}</div></div>
                <div class="mini-panel"><div class="meta-label">modified</div><div class="mini-value">{E(modified)}</div></div>
              </div>
""");

        if (hasError)
            sb.Append($"              <div class=\"scroll-box\">{E(s!.LastError!)}</div>\n");
        else
            sb.Append("              <div class=\"neutral-box\">\u2014</div>\n");

        sb.Append("            </section>\n");
    }

    // ── Outbox panel ─────────────────────────────────────────────────────────

    private static void WriteOutboxPanel(StringBuilder sb, ConsumerTimelineOutbox? o) {
        var outcome   = o?.Outcome ?? "\u2014";
        var status    = o?.Status  ?? "\u2014";
        var nextRetry = o?.NextRetryAt != null ? FmtFull(o.NextRetryAt.Value.UtcDateTime) : "\u2014";
        var modified  = o != null ? FmtFull(o.Modified) : "\u2014";
        var hasError  = !string.IsNullOrWhiteSpace(o?.LastError);

        sb.Append($"""
            <section class="section-box">
              <div class="section-title">outbox</div>
              <div class="summary-grid">
                <div class="mini-panel {Tone(o?.Outcome)}"><div class="meta-label">outcome</div><div class="mini-value">{E(outcome)}</div></div>
                <div class="mini-panel {Tone(o?.Status)}"><div class="meta-label">status</div><div class="mini-value">{E(status)}</div></div>
                <div class="mini-panel"><div class="meta-label">next_retry_at</div><div class="mini-value">{E(nextRetry)}</div></div>
                <div class="mini-panel"><div class="meta-label">modified</div><div class="mini-value">{E(modified)}</div></div>
              </div>
""");

        if (hasError)
            sb.Append($"              <div class=\"scroll-box\">{E(o!.LastError!)}</div>\n");
        else
            sb.Append("              <div class=\"neutral-box\">\u2014</div>\n");

        sb.Append("            </section>\n");
    }

    // ── Actions section ──────────────────────────────────────────────────────

    private static void WriteActionsSection(StringBuilder sb, IReadOnlyList<ConsumerTimelineAction>? actions) {
        sb.Append("""
          <section class="section-box actions-box">
            <div class="section-title">business actions</div>
""");

        if (actions == null || actions.Count == 0) {
            sb.Append("            <div class=\"neutral-box\">\u2014</div>\n");
        } else {
            sb.Append("""
            <div class="table-wrap">
              <table>
                <thead>
                  <tr><th>action_code</th><th>delivery</th><th>business</th><th>started_at</th><th>completed_at</th><th>error</th></tr>
                </thead>
                <tbody>
""");
            for (var i = 0; i < actions.Count; i++) {
                var a = actions[i];
                sb.Append($"                  <tr>" +
                    $"<td>{E(a.ActionCode.ToString())}</td>" +
                    $"<td>{E(a.DeliveryStatus)}</td>" +
                    $"<td>{E(a.BusinessStatus)}</td>" +
                    $"<td>{E(a.StartedAt.HasValue ? FmtFull(a.StartedAt.Value) : "\u2014")}</td>" +
                    $"<td>{E(a.CompletedAt.HasValue ? FmtFull(a.CompletedAt.Value) : "\u2014")}</td>" +
                    $"<td>{E(a.DeliveryError ?? "")}</td>" +
                    $"</tr>\n");
            }
            sb.Append("                </tbody>\n              </table>\n            </div>\n");
        }

        sb.Append("          </section>\n");
    }

    // ── Outbox history section ────────────────────────────────────────────────

    private static void WriteOutboxHistorySection(StringBuilder sb, IReadOnlyList<ConsumerTimelineOutboxHistory>? history) {
        sb.Append("""
          <section class="section-box">
            <div class="section-title">outbox_history</div>
""");

        if (history == null || history.Count == 0) {
            sb.Append("            <div class=\"empty\" style=\"margin-top:12px\">\u2014</div>\n");
        } else {
            sb.Append("""
            <div class="table-wrap">
              <table>
                <thead>
                  <tr><th>attempt_no</th><th>outcome</th><th>status</th><th>created_at</th><th>error</th></tr>
                </thead>
                <tbody>
""");
            for (var i = 0; i < history.Count; i++) {
                var h = history[i];
                sb.Append($"                  <tr>" +
                    $"<td>{E(h.AttemptNo.ToString())}</td>" +
                    $"<td>{E(h.Outcome)}</td>" +
                    $"<td>{E(h.Status)}</td>" +
                    $"<td>{E(FmtFull(h.CreatedAt))}</td>" +
                    $"<td>{E(h.Error ?? "")}</td>" +
                    $"</tr>\n");
            }
            sb.Append("                </tbody>\n              </table>\n            </div>\n");
        }

        sb.Append("          </section>\n");
    }

    // ── Small HTML builders ───────────────────────────────────────────────────

    private static string RailIdRow(string key, string val) =>
        $"<div><span class=\"prop-key\" style=\"display:block\">{E(key)}</span>" +
        $"<span class=\"mono\" style=\"font-size:.82rem;overflow-wrap:anywhere\">{E(val)}</span></div>";

    private static string Stat(string value, string label) =>
        $"<div class=\"stat\"><strong>{E(value)}</strong><div style=\"font-size:.8rem;color:var(--muted)\">{E(label)}</div></div>";

    private static string Prop(string key, string val) =>
        $"<span class=\"prop\"><span class=\"prop-key\">{E(key)}</span><span class=\"prop-val\">{E(val)}</span></span>";

    private static string Tone(string? status) {
        var key = (status ?? string.Empty).ToLowerInvariant();
        if (key.Contains("fail"))                              return "fail";
        if (key.Contains("retry") || key.Contains("pending")) return "warn";
        if (key.Contains("confirm") || key.Contains("process")) return "good";
        return "info";
    }

    // ── Formatters ───────────────────────────────────────────────────────────

    private static string FmtFull(DateTime dt) {
        if (dt == default) return "\u2014";
        var local = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
        return local.ToString("MMM d, yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
    }

    private static string E(string s) => WebUtility.HtmlEncode(s);

    // ── Aggregate helpers ─────────────────────────────────────────────────────

    private static int CountWhere(IReadOnlyList<ConsumerTimelineItem> items, Func<ConsumerTimelineItem, bool> pred) {
        var n = 0;
        for (var i = 0; i < items.Count; i++)
            if (pred(items[i])) n++;
        return n;
    }

    private static int SumInt(IReadOnlyList<ConsumerTimelineItem> items, Func<ConsumerTimelineItem, int> sel) {
        var n = 0;
        for (var i = 0; i < items.Count; i++)
            n += sel(items[i]);
        return n;
    }

    private static int MaxInt(IReadOnlyList<ConsumerTimelineItem> items, Func<ConsumerTimelineItem, int> sel) {
        var max = 0;
        for (var i = 0; i < items.Count; i++) {
            var v = sel(items[i]);
            if (v > max) max = v;
        }
        return max;
    }
}
