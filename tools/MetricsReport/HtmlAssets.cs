namespace CityBuilder.MetricsReport;

/// <summary>
/// The style and the one script written into the report page (docs/plan/CR-0009-T2-PLAN.md §3.4): nothing is loaded from anywhere, the
/// colours follow the system's light or dark mode, and the page reads fine with scripts off (the script only sorts and filters).
/// </summary>
internal static class HtmlAssets
{
    /// <summary>The page's style: light and dark colours, tables, the small-sample mark and the bars.</summary>
    public const string Style = """
        :root { color-scheme: light dark; --bg: #ffffff; --fg: #1d1d1f; --muted: #6e6e73; --line: #d2d2d7; --head: #f5f5f7; --bar: #3b82f6; --mark: #b45309; }
        @media (prefers-color-scheme: dark) { :root { --bg: #161618; --fg: #f5f5f7; --muted: #a1a1a6; --line: #3a3a3c; --head: #232326; --bar: #60a5fa; --mark: #fbbf24; } }
        body { background: var(--bg); color: var(--fg); font: 14px/1.45 system-ui, sans-serif; margin: 0 auto; padding: 16px; max-width: 1600px; }
        h1 { font-size: 22px; } h2 { font-size: 18px; margin-top: 32px; border-bottom: 1px solid var(--line); } h3 { font-size: 15px; }
        nav a { margin-right: 12px; } .note { color: var(--muted); max-width: 900px; }
        .scroll { overflow-x: auto; }
        table { border-collapse: collapse; margin: 8px 0 16px; }
        th, td { border-bottom: 1px solid var(--line); padding: 4px 8px; text-align: left; vertical-align: top; }
        th { background: var(--head); position: sticky; top: 0; cursor: pointer; user-select: none; }
        th[aria-sort="descending"]::after { content: " ▼"; } th[aria-sort="ascending"]::after { content: " ▲"; }
        .n { text-align: right; white-space: nowrap; }
        .small { color: var(--mark); cursor: help; } .tag { color: var(--mark); font-size: 12px; margin-left: 6px; }
        .bar { display: block; height: 4px; background: var(--line); margin-top: 2px; } .bar > span { display: block; height: 4px; background: var(--bar); }
        code { font-size: 12px; color: var(--muted); } details ol { margin: 4px 0 8px; padding-left: 20px; } details p { margin: 6px 0 0; }
        dl { display: grid; grid-template-columns: max-content auto; gap: 2px 16px; } dt { color: var(--muted); } dd { margin: 0; }
        .controls { margin: 8px 0; } .controls label { margin-left: 16px; }
        """;

    /// <summary>
    /// The page's script: a click (or Enter) on a header sorts its table by that column, descending first, then ascending; the
    /// explorer's text box and "named only" switch hide rows. Numbers sort by their <c>data-sort</c> key, an empty key below all.
    /// </summary>
    public const string Script = """
        (function () {
          function key(cell, numeric) {
            var value = cell.getAttribute('data-sort');
            return numeric ? (value === '' ? -Infinity : Number(value)) : value;
          }
          Array.prototype.forEach.call(document.querySelectorAll('table.sortable'), function (table) {
            var heads = table.tHead.rows[0].cells;
            Array.prototype.forEach.call(heads, function (th, column) {
              th.tabIndex = 0;
              function sort() {
                var numeric = th.getAttribute('data-type') === 'num';
                var descending = th.getAttribute('aria-sort') !== 'descending';
                Array.prototype.forEach.call(heads, function (other) { other.removeAttribute('aria-sort'); });
                th.setAttribute('aria-sort', descending ? 'descending' : 'ascending');
                var body = table.tBodies[0];
                var rows = Array.prototype.slice.call(body.rows);
                rows.sort(function (a, b) {
                  var x = key(a.cells[column], numeric), y = key(b.cells[column], numeric);
                  var order = numeric ? (x < y ? -1 : (x > y ? 1 : 0)) : x.localeCompare(y);
                  return descending ? -order : order;
                });
                rows.forEach(function (row) { body.appendChild(row); });
              }
              th.addEventListener('click', sort);
              th.addEventListener('keydown', function (e) {
                if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); sort(); }
              });
            });
          });
          var filter = document.getElementById('explorer-filter');
          var named = document.getElementById('explorer-named');
          var table = document.getElementById('explorer-table');
          function apply() {
            var text = filter.value.toLowerCase();
            Array.prototype.forEach.call(table.tBodies[0].rows, function (row) {
              var title = row.cells[0].getAttribute('data-sort').toLowerCase();
              row.hidden = (named.checked && row.getAttribute('data-kind') !== 'named') || title.indexOf(text) < 0;
            });
          }
          if (filter && named && table) {
            filter.addEventListener('input', apply);
            named.addEventListener('change', apply);
          }
        })();
        """;
}
