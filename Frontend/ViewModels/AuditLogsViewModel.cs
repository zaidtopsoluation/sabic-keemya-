using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keemya.Frontend.Stores;
using MySqlConnector;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Keemya.Frontend.ViewModels
{
    public class AuditLogItem
    {
        public string Id { get; set; }
        public string Actor { get; set; }
        public string Action { get; set; }
        public string Description { get; set; }
        public string Module { get; set; }
        public string Timestamp { get; set; }
        public string EntityId { get; set; }
        public DateTime RawTimestamp { get; set; }
    }

    public partial class AuditLogsViewModel : ObservableObject
    {
        private readonly NavigationStore _navigationStore;
        private readonly string _connectionString = AppConfig.ConnectionString;

        private List<AuditLogItem> _allLogs = new();
        private List<AuditLogItem> _filteredLogs = new();

        // KPI Properties
        [ObservableProperty] private string _totalLogs = "0";
        [ObservableProperty] private string _todayLogs = "0";
        [ObservableProperty] private string _thisWeekLogs = "0";
        [ObservableProperty] private string _uniqueUsers = "0";

        // Filter Properties
        [ObservableProperty] private string _searchQuery = "";
        
        [ObservableProperty] private ObservableCollection<string> _modulesList;
        [ObservableProperty] private string _selectedModule = "All Modules";

        [ObservableProperty] private ObservableCollection<string> _usersList;
        [ObservableProperty] private string _selectedUser = "All Users";

        [ObservableProperty] private DateTime? _startDate;
        [ObservableProperty] private DateTime? _endDate;

        partial void OnSearchQueryChanged(string value) => FilterLogs();
        partial void OnSelectedModuleChanged(string value) => FilterLogs();
        partial void OnSelectedUserChanged(string value) => FilterLogs();
        partial void OnStartDateChanged(DateTime? value) => FilterLogs();
        partial void OnEndDateChanged(DateTime? value) => FilterLogs();

        // Pagination Properties
        [ObservableProperty] private int _itemsPerPage = 20;
        partial void OnItemsPerPageChanged(int value) 
        {
            CurrentPage = 1;
            UpdatePagination();
        }
        
        [ObservableProperty] private ObservableCollection<int> _itemsPerPageOptions = new() { 10, 20, 50 };

        [ObservableProperty] private int _currentPage = 1;
        [ObservableProperty] private int _totalPages = 1;
        
        [ObservableProperty] private string _paginationInfo = "Page 1 of 1";
        [ObservableProperty] private string _totalRowsInfo = "0 row(s) found";

        [ObservableProperty] private bool _canGoFirst;
        [ObservableProperty] private bool _canGoPrevious;
        [ObservableProperty] private bool _canGoNext;
        [ObservableProperty] private bool _canGoLast;

        // Data Properties
        [ObservableProperty] private ObservableCollection<AuditLogItem> _logs = new();

        // Modal Properties
        [ObservableProperty] private bool _isLogDetailsPopupOpen = false;
        [ObservableProperty] private AuditLogItem _selectedLog;

        public AuditLogsViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;

            ModulesList = new ObservableCollection<string> { "All Modules" };
            UsersList = new ObservableCollection<string> { "All Users" };

            _ = LoadLogsAsync();
        }

        private async Task LoadLogsAsync()
        {
            try
            {
                var loadedLogs = new List<AuditLogItem>();
                var modules = new HashSet<string>();
                var users = new HashSet<string>();

                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT id, actor, action, description, module, entity_id, timestamp FROM audit_logs ORDER BY timestamp DESC";
                    
                    using (var command = new MySqlCommand(query, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var timestamp = reader.GetDateTime(6);
                            var log = new AuditLogItem
                            {
                                Id = reader.GetInt32(0).ToString(),
                                Actor = reader.GetString(1),
                                Action = reader.GetString(2),
                                Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Module = reader.GetString(4),
                                EntityId = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                RawTimestamp = timestamp,
                                Timestamp = timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                            };

                            loadedLogs.Add(log);
                            modules.Add(log.Module);
                            users.Add(log.Actor);
                        }
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _allLogs = loadedLogs;
                    
                    // Update dropdowns
                    ModulesList.Clear();
                    ModulesList.Add("All Modules");
                    foreach (var m in modules.OrderBy(x => x)) ModulesList.Add(m);

                    UsersList.Clear();
                    UsersList.Add("All Users");
                    foreach (var u in users.OrderBy(x => x)) UsersList.Add(u);

                    CalculateKpis();
                    FilterLogs();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading audit logs: " + ex.Message);
            }
        }

        private void CalculateKpis()
        {
            TotalLogs = _allLogs.Count.ToString();
            
            var today = DateTime.UtcNow.Date;
            TodayLogs = _allLogs.Count(x => x.RawTimestamp.Date == today).ToString();

            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            ThisWeekLogs = _allLogs.Count(x => x.RawTimestamp.Date >= startOfWeek).ToString();

            UniqueUsers = _allLogs.Select(x => x.Actor).Distinct().Count().ToString();
        }

        private void FilterLogs()
        {
            var filtered = _allLogs.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.ToLower();
                filtered = filtered.Where(x => 
                    x.Action.ToLower().Contains(q) || 
                    x.Description.ToLower().Contains(q) || 
                    x.Actor.ToLower().Contains(q) ||
                    x.EntityId.ToLower().Contains(q));
            }

            if (SelectedModule != "All Modules" && !string.IsNullOrEmpty(SelectedModule))
            {
                filtered = filtered.Where(x => x.Module == SelectedModule);
            }

            if (SelectedUser != "All Users" && !string.IsNullOrEmpty(SelectedUser))
            {
                filtered = filtered.Where(x => x.Actor == SelectedUser);
            }

            if (StartDate.HasValue)
            {
                filtered = filtered.Where(x => x.RawTimestamp.Date >= StartDate.Value.Date);
            }

            if (EndDate.HasValue)
            {
                filtered = filtered.Where(x => x.RawTimestamp.Date <= EndDate.Value.Date);
            }

            _filteredLogs = filtered.ToList();
            CurrentPage = 1;
            UpdatePagination();
        }

        private void UpdatePagination()
        {
            int totalItems = _filteredLogs.Count;
            TotalRowsInfo = $"{totalItems} row(s) found";

            TotalPages = (int)Math.Ceiling(totalItems / (double)ItemsPerPage);
            if (TotalPages == 0) TotalPages = 1;

            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            PaginationInfo = $"Page {CurrentPage} of {TotalPages}";

            CanGoFirst = CurrentPage > 1;
            CanGoPrevious = CurrentPage > 1;
            CanGoNext = CurrentPage < TotalPages;
            CanGoLast = CurrentPage < TotalPages;

            var paged = _filteredLogs
                .Skip((CurrentPage - 1) * ItemsPerPage)
                .Take(ItemsPerPage)
                .ToList();

            Logs.Clear();
            foreach (var item in paged)
            {
                Logs.Add(item);
            }
        }

        [RelayCommand]
        private void FirstPage()
        {
            CurrentPage = 1;
            UpdatePagination();
        }

        [RelayCommand]
        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                UpdatePagination();
            }
        }

        [RelayCommand]
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                UpdatePagination();
            }
        }

        [RelayCommand]
        private void LastPage()
        {
            CurrentPage = TotalPages;
            UpdatePagination();
        }

        [RelayCommand]
        private void NavigateBack()
        {
            _navigationStore.CurrentViewModel = new DashboardViewModel(_navigationStore);
        }

        [RelayCommand]
        private void OpenLogDetails(AuditLogItem log)
        {
            if (log != null)
            {
                SelectedLog = log;
                IsLogDetailsPopupOpen = true;
            }
        }

        [RelayCommand]
        private void CloseLogDetails()
        {
            IsLogDetailsPopupOpen = false;
        }

        [RelayCommand]
        private void ExportCsv()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "AuditLogs.xlsx",
                    DefaultExt = ".xlsx",
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV File (*.csv)|*.csv|All files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                    if (ext == ".csv")
                    {
                        ExportToCsvFile(dialog.FileName);
                    }
                    else
                    {
                        ExportToXlsxFile(dialog.FileName);
                    }
                    MessageBox.Show("Export completed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToXlsxFile(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

            // 1. [Content_Types].xml
            var contentTypesEntry = archive.CreateEntry("[Content_Types].xml");
            using (var writer = new StreamWriter(contentTypesEntry.Open(), System.Text.Encoding.UTF8))
            {
                writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
</Types>");
            }

            // 2. _rels/.rels
            var relsEntry = archive.CreateEntry("_rels/.rels");
            using (var writer = new StreamWriter(relsEntry.Open(), System.Text.Encoding.UTF8))
            {
                writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>");
            }

            // 3. xl/workbook.xml
            var workbookEntry = archive.CreateEntry("xl/workbook.xml");
            using (var writer = new StreamWriter(workbookEntry.Open(), System.Text.Encoding.UTF8))
            {
                writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
    <sheet name=""Audit Logs"" sheetId=""1"" r:id=""rId1""/>
  </sheets>
</workbook>");
            }

            // 4. xl/_rels/workbook.xml.rels
            var workbookRelsEntry = archive.CreateEntry("xl/_rels/workbook.xml.rels");
            using (var writer = new StreamWriter(workbookRelsEntry.Open(), System.Text.Encoding.UTF8))
            {
                writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
</Relationships>");
            }

            // 5. xl/styles.xml (font 1 is bold, style 1 uses font 1)
            var stylesEntry = archive.CreateEntry("xl/styles.xml");
            using (var writer = new StreamWriter(stylesEntry.Open(), System.Text.Encoding.UTF8))
            {
                writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""2"">
    <font>
      <sz val=""11""/>
      <name val=""Arial""/>
    </font>
    <font>
      <b/>
      <sz val=""11""/>
      <name val=""Arial""/>
    </font>
  </fonts>
  <fills count=""2"">
    <fill><patternFill patternType=""none""/></fill>
    <fill><patternFill patternType=""gray125""/></fill>
  </fills>
  <borders count=""1"">
    <border><left/><right/><top/><bottom/><diagonal/></border>
  </borders>
  <cellStyleXfs count=""1"">
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/>
  </cellStyleXfs>
  <cellXfs count=""2"">
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
    <xf numFmtId=""0"" fontId=""1"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1""/>
  </cellXfs>
</styleSheet>");
            }

            // 6. xl/worksheets/sheet1.xml
            var sheetEntry = archive.CreateEntry("xl/worksheets/sheet1.xml");
            using (var writer = new StreamWriter(sheetEntry.Open(), System.Text.Encoding.UTF8))
            {
                writer.WriteLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
                writer.WriteLine(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">");

                // Freeze Pane on Row 1
                writer.WriteLine(@"  <sheetViews>");
                writer.WriteLine(@"    <sheetView tabSelected=""1"" workbookViewId=""0"">");
                writer.WriteLine(@"      <pane ySplit=""1"" topLeftCell=""A2"" activePane=""bottomLeft"" state=""frozen""/>");
                writer.WriteLine(@"    </sheetView>");
                writer.WriteLine(@"  </sheetViews>");

                // Custom Column Widths (6 columns: Timestamp, User, Action, Module, Entity, Description)
                writer.WriteLine(@"  <cols>");
                writer.WriteLine(@"    <col min=""1"" max=""1"" width=""24"" customWidth=""1""/>");
                writer.WriteLine(@"    <col min=""2"" max=""2"" width=""20"" customWidth=""1""/>");
                writer.WriteLine(@"    <col min=""3"" max=""3"" width=""20"" customWidth=""1""/>");
                writer.WriteLine(@"    <col min=""4"" max=""4"" width=""20"" customWidth=""1""/>");
                writer.WriteLine(@"    <col min=""5"" max=""5"" width=""18"" customWidth=""1""/>");
                writer.WriteLine(@"    <col min=""6"" max=""6"" width=""60"" customWidth=""1""/>");
                writer.WriteLine(@"  </cols>");

                writer.WriteLine(@"  <sheetData>");

                // Row 1: Bold Header (s="1")
                writer.WriteLine(@"    <row r=""1"">");
                writer.WriteLine(@"      <c r=""A1"" t=""inlineStr"" s=""1""><is><t>Timestamp</t></is></c>");
                writer.WriteLine(@"      <c r=""B1"" t=""inlineStr"" s=""1""><is><t>User</t></is></c>");
                writer.WriteLine(@"      <c r=""C1"" t=""inlineStr"" s=""1""><is><t>Action</t></is></c>");
                writer.WriteLine(@"      <c r=""D1"" t=""inlineStr"" s=""1""><is><t>Module</t></is></c>");
                writer.WriteLine(@"      <c r=""E1"" t=""inlineStr"" s=""1""><is><t>Entity</t></is></c>");
                writer.WriteLine(@"      <c r=""F1"" t=""inlineStr"" s=""1""><is><t>Description</t></is></c>");
                writer.WriteLine(@"    </row>");

                int rowIndex = 2;
                foreach (var log in _filteredLogs)
                {
                    writer.WriteLine($"    <row r=\"{rowIndex}\">");
                    writer.WriteLine($"      <c r=\"A{rowIndex}\" t=\"inlineStr\"><is><t>{EscapeXml(log.Timestamp)}</t></is></c>");
                    writer.WriteLine($"      <c r=\"B{rowIndex}\" t=\"inlineStr\"><is><t>{EscapeXml(log.Actor)}</t></is></c>");
                    writer.WriteLine($"      <c r=\"C{rowIndex}\" t=\"inlineStr\"><is><t>{EscapeXml(log.Action)}</t></is></c>");
                    writer.WriteLine($"      <c r=\"D{rowIndex}\" t=\"inlineStr\"><is><t>{EscapeXml(log.Module)}</t></is></c>");
                    writer.WriteLine($"      <c r=\"E{rowIndex}\" t=\"inlineStr\"><is><t>{EscapeXml(log.EntityId)}</t></is></c>");
                    writer.WriteLine($"      <c r=\"F{rowIndex}\" t=\"inlineStr\"><is><t>{EscapeXml(log.Description)}</t></is></c>");
                    writer.WriteLine(@"    </row>");
                    rowIndex++;
                }
                writer.WriteLine(@"  </sheetData>");

                // AutoFilter across A1 to F{lastRow}
                int lastRow = Math.Max(1, rowIndex - 1);
                writer.WriteLine($"  <autoFilter ref=\"A1:F{lastRow}\"/>");

                writer.WriteLine(@"</worksheet>");
            }
        }

        private void ExportToCsvFile(string filePath)
        {
            using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            writer.WriteLine("Timestamp,User,Action,Module,Entity,Description");
            foreach (var log in _filteredLogs)
            {
                var desc = log.Description?.Replace("\"", "\"\"") ?? "";
                writer.WriteLine($"{log.Timestamp},{log.Actor},{log.Action},{log.Module},{log.EntityId},\"{desc}\"");
            }
        }

        private static string EscapeXml(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return SecurityElement.Escape(input) ?? "";
        }

        [RelayCommand]
        private void ExportPdf()
        {
            try
            {
                QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "AuditLogs.pdf",
                    DefaultExt = ".pdf",
                    Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    QuestPDF.Fluent.Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(QuestPDF.Helpers.PageSizes.A4.Landscape());
                            page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);
                            page.PageColor(QuestPDF.Helpers.Colors.White);
                            page.DefaultTextStyle(x => x.FontSize(10));

                            page.Header().Element(ComposeHeader);
                            page.Content().Element(ComposeContent);
                            page.Footer().AlignCenter().Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                            });
                        });
                    })
                    .GeneratePdf(dialog.FileName);

                    MessageBox.Show("PDF Export completed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ComposeHeader(QuestPDF.Infrastructure.IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Audit Logs Report").FontSize(20).SemiBold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                    column.Item().Text($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    column.Item().Text($"Total Records: {_filteredLogs.Count}");
                });
            });
        }

        private void ComposeContent(QuestPDF.Infrastructure.IContainer container)
        {
            container.PaddingVertical(1, QuestPDF.Infrastructure.Unit.Centimetre).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(120);
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(120);
                    columns.ConstantColumn(70);
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("Timestamp");
                    header.Cell().Element(CellStyle).Text("User");
                    header.Cell().Element(CellStyle).Text("Action");
                    header.Cell().Element(CellStyle).Text("Module");
                    header.Cell().Element(CellStyle).Text("Entity");
                    header.Cell().Element(CellStyle).Text("Description");

                    static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                    {
                        return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Black);
                    }
                });

                foreach (var log in _filteredLogs)
                {
                    table.Cell().Element(CellStyle).Text(log.Timestamp);
                    table.Cell().Element(CellStyle).Text(log.Actor);
                    table.Cell().Element(CellStyle).Text(log.Action);
                    table.Cell().Element(CellStyle).Text(log.Module);
                    table.Cell().Element(CellStyle).Text(log.EntityId);
                    table.Cell().Element(CellStyle).Text(log.Description);

                    static QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                    {
                        return container.BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).PaddingVertical(5);
                    }
                }
            });
        }
    }
}
