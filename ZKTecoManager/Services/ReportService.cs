using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using ZKTecoManager.Data;
using ZKTecoManager.Models.Entities;
using ZKTecoManager.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ZKTecoManager.Services;

public class ReportService : IReportService
{
    private readonly IAttendanceService _attendanceSvc;
    private readonly IServiceScopeFactory _scopeFactory;

    public ReportService(IAttendanceService attendanceSvc, IServiceScopeFactory scopeFactory)
    {
        _attendanceSvc = attendanceSvc;
        _scopeFactory  = scopeFactory;
    }

    // ── Excel ─────────────────────────────────────────────────────────────────

    public async Task<string> ExportExcelAsync(ReportOptions opts, string filePath, CancellationToken ct = default)
    {
        var records = await LoadRecordsAsync(opts, ct);

        using var wb = new XLWorkbook();

        if (opts.Type == ReportType.AttendanceDetail)
            BuildDetailSheet(wb, records, opts);
        else
            BuildSummarySheet(wb, records, opts);

        await Task.Run(() => wb.SaveAs(filePath), ct);
        return filePath;
    }

    // ── PDF ───────────────────────────────────────────────────────────────────

    public async Task<string> ExportPdfAsync(ReportOptions opts, string filePath, CancellationToken ct = default)
    {
        var records = await LoadRecordsAsync(opts, ct);

        await Task.Run(() =>
        {
            var doc = new PdfDocument();
            doc.Info.Title = opts.Type == ReportType.AttendanceDetail
                ? "Reporte de Asistencia" : "Resumen Mensual";

            if (opts.Type == ReportType.AttendanceDetail)
                BuildDetailPdf(doc, records, opts);
            else
                BuildSummaryPdf(doc, records, opts);

            doc.Save(filePath);
        }, ct);

        return filePath;
    }

    // ── Data loader ───────────────────────────────────────────────────────────

    private async Task<List<AttendanceRecord>> LoadRecordsAsync(ReportOptions opts, CancellationToken ct)
    {
        var filter = new AttendanceFilter(opts.CompanyId, opts.DepartmentId, opts.EmployeeId, opts.From, opts.To);
        var result = await _attendanceSvc.GetRecordsAsync(filter, ct);
        return result.ToList();
    }

    // ── Excel builders ────────────────────────────────────────────────────────

    private static void BuildDetailSheet(XLWorkbook wb, List<AttendanceRecord> records, ReportOptions opts)
    {
        var ws = wb.Worksheets.Add("Asistencia");
        int row = 1;

        // Title block
        SetTitle(ws, row++, "REPORTE DE ASISTENCIA", 13);
        ws.Cell(row, 1).Value = $"Período: {opts.From:dd/MM/yyyy}  al  {opts.To:dd/MM/yyyy}";
        MergeRow(ws, row++, 13);
        ws.Cell(row, 1).Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
        MergeRow(ws, row++, 13, italic: true);
        row++; // blank

        // Headers
        string[] headers = ["Fecha", "Día", "Código", "Nombre", "Departamento",
                            "Turno", "Entrada", "Salida", "Horas", "Tard.(min)",
                            "H.Extra", "Estado", "Notas"];
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        row++;

        // Data
        bool alt = false;
        foreach (var r in records)
        {
            var bg = r.Status switch
            {
                AttendanceStatus.Absent    => XLColor.FromHtml("#FEE2E2"),
                AttendanceStatus.Late      => XLColor.FromHtml("#FEF3C7"),
                AttendanceStatus.Justified => XLColor.FromHtml("#DBEAFE"),
                AttendanceStatus.DayOff    => XLColor.FromHtml("#F3F4F6"),
                _ => alt ? XLColor.FromHtml("#F9FAFB") : XLColor.White
            };
            alt = !alt;

            ws.Cell(row, 1).Value  = r.WorkDate.ToString("dd/MM/yyyy");
            ws.Cell(row, 2).Value  = r.WorkDate.ToDateTime(TimeOnly.MinValue).ToString("ddd");
            ws.Cell(row, 3).Value  = r.Employee.EmployeeCode;
            ws.Cell(row, 4).Value  = r.Employee.FullName;
            ws.Cell(row, 5).Value  = r.Employee.Department?.Name ?? "";
            ws.Cell(row, 6).Value  = r.Shift?.Name ?? "";
            ws.Cell(row, 7).Value  = r.CheckIn.HasValue  ? r.CheckIn.Value.ToLocalTime().ToString("HH:mm")  : "";
            ws.Cell(row, 8).Value  = r.CheckOut.HasValue ? r.CheckOut.Value.ToLocalTime().ToString("HH:mm") : "";
            ws.Cell(row, 9).Value  = r.HoursWorked.HasValue ? (double)r.HoursWorked.Value : 0d;
            ws.Cell(row, 10).Value = r.LateMinutes;
            ws.Cell(row, 11).Value = (double)r.OvertimeHours;
            ws.Cell(row, 12).Value = StatusLabel(r.Status);
            ws.Cell(row, 13).Value = r.Notes ?? "";

            ws.Range(row, 1, row, 13).Style.Fill.BackgroundColor = bg;
            row++;
        }

        // Totals
        ws.Cell(row, 8).Value = "TOTAL HORAS:";
        ws.Cell(row, 8).Style.Font.Bold = true;
        ws.Cell(row, 9).Value = records.Sum(r => (double)(r.HoursWorked ?? 0));
        ws.Cell(row, 9).Style.Font.Bold = true;
        ws.Cell(row, 10).Value = records.Sum(r => r.LateMinutes);
        ws.Cell(row, 10).Style.Font.Bold = true;
        ws.Cell(row, 11).Value = (double)records.Sum(r => r.OvertimeHours);
        ws.Cell(row, 11).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
        ws.Column(4).Width = 28;  // Name can be wide
        ws.Column(13).Width = 30; // Notes
    }

    private static void BuildSummarySheet(XLWorkbook wb, List<AttendanceRecord> records, ReportOptions opts)
    {
        var ws = wb.Worksheets.Add("Resumen");
        int row = 1;

        SetTitle(ws, row++, "RESUMEN MENSUAL DE ASISTENCIA", 9);
        ws.Cell(row, 1).Value = $"Período: {opts.From:dd/MM/yyyy}  al  {opts.To:dd/MM/yyyy}";
        MergeRow(ws, row++, 9);
        row++;

        string[] headers = ["Código", "Nombre", "Departamento",
                            "Presentes", "Ausentes", "Tardanzas", "Justificados",
                            "Total Horas", "H.Extra"];
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        row++;

        var groups = records
            .GroupBy(r => r.EmployeeId)
            .Select(g => (
                Emp:       g.First().Employee,
                Present:   g.Count(r => r.Status is AttendanceStatus.Normal or AttendanceStatus.Late),
                Absent:    g.Count(r => r.Status == AttendanceStatus.Absent),
                Late:      g.Count(r => r.Status == AttendanceStatus.Late),
                Justified: g.Count(r => r.Status == AttendanceStatus.Justified),
                Hours:     g.Sum(r => r.HoursWorked ?? 0),
                Overtime:  g.Sum(r => r.OvertimeHours)
            ))
            .OrderBy(x => x.Emp.LastName);

        bool alt = false;
        foreach (var s in groups)
        {
            var bg = alt ? XLColor.FromHtml("#F9FAFB") : XLColor.White;
            alt = !alt;

            ws.Cell(row, 1).Value = s.Emp.EmployeeCode;
            ws.Cell(row, 2).Value = s.Emp.FullName;
            ws.Cell(row, 3).Value = s.Emp.Department?.Name ?? "";
            ws.Cell(row, 4).Value = s.Present;
            ws.Cell(row, 5).Value = s.Absent;
            ws.Cell(row, 6).Value = s.Late;
            ws.Cell(row, 7).Value = s.Justified;
            ws.Cell(row, 8).Value = (double)s.Hours;
            ws.Cell(row, 9).Value = (double)s.Overtime;

            ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = bg;
            row++;
        }

        // Grand totals
        var totRow = ws.Range(row, 1, row, 9);
        totRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        totRow.Style.Font.FontColor = XLColor.White;
        totRow.Style.Font.Bold = true;
        ws.Cell(row, 2).Value = "TOTALES";
        ws.Cell(row, 4).Value = records.Count(r => r.Status is AttendanceStatus.Normal or AttendanceStatus.Late);
        ws.Cell(row, 5).Value = records.Count(r => r.Status == AttendanceStatus.Absent);
        ws.Cell(row, 6).Value = records.Count(r => r.Status == AttendanceStatus.Late);
        ws.Cell(row, 7).Value = records.Count(r => r.Status == AttendanceStatus.Justified);
        ws.Cell(row, 8).Value = (double)records.Sum(r => r.HoursWorked ?? 0);
        ws.Cell(row, 9).Value = (double)records.Sum(r => r.OvertimeHours);

        ws.Columns().AdjustToContents();
    }

    // ── PDF builders ──────────────────────────────────────────────────────────

    private static void BuildDetailPdf(PdfDocument doc, List<AttendanceRecord> records, ReportOptions opts)
    {
        const int rowsPerPage = 28;
        var chunks = records.Chunk(rowsPerPage).ToList();
        if (chunks.Count == 0) chunks = [[]];

        string[] headers = ["Fecha", "Código", "Nombre", "Dpto.", "Turno",
                            "Entrada", "Salida", "Horas", "Tard.", "H.E.", "Estado"];
        double[] widths  = [60, 50, 130, 90, 70, 45, 45, 40, 38, 38, 65];

        int totalPages = chunks.Count;
        int pageNum = 0;

        foreach (var chunk in chunks)
        {
            pageNum++;
            var page = doc.AddPage();
            page.Orientation = PdfSharpCore.PageOrientation.Landscape;
            page.Size = PdfSharpCore.PageSize.A4;
            var gfx = XGraphics.FromPdfPage(page);

            double marginL = 28, marginT = 36;
            double y = marginT;

            // Title
            var titleFont = new XFont("Arial", 13, XFontStyle.Bold);
            gfx.DrawString("REPORTE DE ASISTENCIA", titleFont, XBrushes.Black,
                new XPoint(marginL, y));
            y += 18;

            var subFont = new XFont("Arial", 8, XFontStyle.Regular);
            gfx.DrawString(
                $"Período: {opts.From:dd/MM/yyyy} – {opts.To:dd/MM/yyyy}   |   " +
                $"Pág. {pageNum}/{totalPages}   |   Generado: {DateTime.Now:dd/MM/yyyy HH:mm}",
                subFont, XBrushes.DarkGray, new XPoint(marginL, y));
            y += 14;

            // Table header
            DrawPdfTableHeader(gfx, marginL, y, headers, widths);
            y += 16;

            // Rows
            var rowFont = new XFont("Arial", 7.5, XFontStyle.Regular);
            bool alt = false;
            foreach (var r in chunk)
            {
                var fillBrush = r.Status switch
                {
                    AttendanceStatus.Absent    => new XSolidBrush(XColor.FromArgb(255, 254, 226, 226)),
                    AttendanceStatus.Late      => new XSolidBrush(XColor.FromArgb(255, 254, 243, 199)),
                    AttendanceStatus.Justified => new XSolidBrush(XColor.FromArgb(255, 219, 234, 254)),
                    _ => alt ? new XSolidBrush(XColor.FromArgb(255, 249, 250, 251)) : XBrushes.White
                };
                alt = !alt;

                double x = marginL;
                var totalW = widths.Sum();
                gfx.DrawRectangle(fillBrush, x, y - 10, totalW, 14);

                string[] cells =
                [
                    r.WorkDate.ToString("dd/MM/yy"),
                    r.Employee.EmployeeCode,
                    Truncate(r.Employee.FullName, 22),
                    Truncate(r.Employee.Department?.Name ?? "", 15),
                    Truncate(r.Shift?.Name ?? "—", 12),
                    r.CheckIn.HasValue  ? r.CheckIn.Value.ToLocalTime().ToString("HH:mm")  : "—",
                    r.CheckOut.HasValue ? r.CheckOut.Value.ToLocalTime().ToString("HH:mm") : "—",
                    r.HoursWorked.HasValue ? $"{r.HoursWorked:F1}" : "—",
                    r.LateMinutes > 0 ? $"{r.LateMinutes}" : "—",
                    r.OvertimeHours > 0 ? $"{r.OvertimeHours:F1}" : "—",
                    StatusLabel(r.Status)
                ];

                for (int c = 0; c < cells.Length; c++)
                {
                    gfx.DrawString(cells[c], rowFont, XBrushes.Black,
                        new XPoint(x + 2, y));
                    x += widths[c];
                }
                y += 14;
            }

            // Footer line
            var footFont = new XFont("Arial", 7, XFontStyle.Italic);
            gfx.DrawLine(XPens.LightGray, marginL, y + 4, marginL + widths.Sum(), y + 4);
            gfx.DrawString($"Total registros en esta página: {chunk.Length}  |  " +
                           $"Total general: {records.Count}",
                footFont, XBrushes.Gray, new XPoint(marginL, y + 14));
        }
    }

    private static void BuildSummaryPdf(PdfDocument doc, List<AttendanceRecord> records, ReportOptions opts)
    {
        var groups = records
            .GroupBy(r => r.EmployeeId)
            .Select(g => new
            {
                Emp       = g.First().Employee,
                Present   = g.Count(r => r.Status is AttendanceStatus.Normal or AttendanceStatus.Late),
                Absent    = g.Count(r => r.Status == AttendanceStatus.Absent),
                Late      = g.Count(r => r.Status == AttendanceStatus.Late),
                Justified = g.Count(r => r.Status == AttendanceStatus.Justified),
                Hours     = g.Sum(r => r.HoursWorked ?? 0),
                Overtime  = g.Sum(r => r.OvertimeHours)
            })
            .OrderBy(x => x.Emp.LastName)
            .ToList();

        const int rowsPerPage = 34;
        var chunks = groups.Chunk(rowsPerPage).ToList();
        if (chunks.Count == 0) chunks = [[]];

        string[] headers = ["Código", "Nombre", "Departamento",
                            "Presentes", "Ausentes", "Tardanzas", "Justif.",
                            "Total Hrs", "H.Extra"];
        double[] widths  = [55, 150, 100, 55, 55, 55, 50, 55, 50];

        int totalPages = chunks.Count, pageNum = 0;

        foreach (var chunk in chunks)
        {
            pageNum++;
            var page = doc.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;
            var gfx = XGraphics.FromPdfPage(page);

            double marginL = 28, y = 36;

            var titleFont = new XFont("Arial", 13, XFontStyle.Bold);
            gfx.DrawString("RESUMEN MENSUAL DE ASISTENCIA", titleFont, XBrushes.Black, new XPoint(marginL, y));
            y += 18;

            var subFont = new XFont("Arial", 8);
            gfx.DrawString(
                $"Período: {opts.From:dd/MM/yyyy} – {opts.To:dd/MM/yyyy}   |   Pág. {pageNum}/{totalPages}",
                subFont, XBrushes.DarkGray, new XPoint(marginL, y));
            y += 14;

            DrawPdfTableHeader(gfx, marginL, y, headers, widths);
            y += 16;

            var rowFont = new XFont("Arial", 8);
            bool alt = false;
            foreach (var s in chunk)
            {
                var fill = alt
                    ? new XSolidBrush(XColor.FromArgb(255, 249, 250, 251))
                    : XBrushes.White;
                alt = !alt;

                gfx.DrawRectangle(fill, marginL, y - 10, widths.Sum(), 14);

                string[] cells =
                [
                    s.Emp.EmployeeCode,
                    Truncate(s.Emp.FullName, 24),
                    Truncate(s.Emp.Department?.Name ?? "", 16),
                    $"{s.Present}", $"{s.Absent}", $"{s.Late}", $"{s.Justified}",
                    $"{s.Hours:F1}", $"{s.Overtime:F1}"
                ];

                double x = marginL;
                for (int c = 0; c < cells.Length; c++)
                {
                    gfx.DrawString(cells[c], rowFont, XBrushes.Black, new XPoint(x + 2, y));
                    x += widths[c];
                }
                y += 14;
            }
        }
    }

    private static void DrawPdfTableHeader(XGraphics gfx, double x, double y,
        string[] headers, double[] widths)
    {
        var hdrFont   = new XFont("Arial", 8, XFontStyle.Bold);
        var hdrBrush  = new XSolidBrush(XColor.FromArgb(255, 30, 58, 95));
        var textBrush = XBrushes.White;
        double totalW = widths.Sum();

        gfx.DrawRectangle(hdrBrush, x, y - 11, totalW, 14);
        double cx = x;
        for (int i = 0; i < headers.Length; i++)
        {
            gfx.DrawString(headers[i], hdrFont, textBrush, new XPoint(cx + 2, y));
            cx += widths[i];
        }
    }

    // ── Excel helpers ─────────────────────────────────────────────────────────

    private static void SetTitle(IXLWorksheet ws, int row, string text, int cols)
    {
        ws.Cell(row, 1).Value = text;
        var range = ws.Range(row, 1, row, cols);
        range.Merge();
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 14;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void MergeRow(IXLWorksheet ws, int row, int cols, bool italic = false)
    {
        var range = ws.Range(row, 1, row, cols);
        range.Merge();
        range.Style.Font.Italic = italic;
        range.Style.Font.FontColor = XLColor.FromHtml("#6B7280");
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static string StatusLabel(AttendanceStatus s) => s switch
    {
        AttendanceStatus.Normal    => "Normal",
        AttendanceStatus.Late      => "Tardanza",
        AttendanceStatus.Absent    => "Falta",
        AttendanceStatus.Justified => "Justificado",
        AttendanceStatus.DayOff    => "Descanso",
        _                          => "Pendiente"
    };

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
