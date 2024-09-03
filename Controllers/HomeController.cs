using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml; // Dodaj to using do obs≥ugi EPPlus

namespace KartaMieszkanca.Controllers
{
    public class ResidentCardController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly string _networkFolderPath;
        private readonly string _localFolderPath;
        private readonly string _excelFilePath;
        private const int MaxPhotoSize = 5 * 1024 * 1024; // Maks. 5 MB
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };

        public ResidentCardController(IWebHostEnvironment env)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            _env = env;
            _networkFolderPath = @"\\10.0.0.176\Karta Mieszkanca\_PODKOWIA—SKA KARTA MIESZKA—CA";
            _localFolderPath = Path.Combine(_env.WebRootPath, "cards");
            _excelFilePath = Path.Combine(_networkFolderPath, "KARTA MIESZKA—CA.xlsx"); // åcieøka do pliku Excel
        }

        [HttpGet]
        public IActionResult Index()
        {
            int lastUsedCardNumber = GetLastUsedCardNumber();
            ViewBag.LastUsedCardNumber = lastUsedCardNumber;
            return View();
        }

        [HttpPost]
        public IActionResult GenerateCard(IFormFile photo, string firstName, string lastName, string cardNumber, DateTime? startDate, DateTime? endDate)
        {
            if (photo == null || string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
            {
                ViewBag.ErrorMessage = "Wszystkie pola sπ wymagane.";
                return View("Index");
            }

            if (!IsValidPhoto(photo))
            {
                ViewBag.ErrorMessage = "Nieprawid≥owy format lub rozmiar zdjÍcia.";
                return View("Index");
            }

            // Konwersja imienia i nazwiska na wielkie litery
            firstName = firstName.ToUpperInvariant();
            lastName = lastName.ToUpperInvariant();

            // Sprawdzanie i ustawianie numeru karty
            int newCardNumber;
            if (string.IsNullOrWhiteSpace(cardNumber))
            {
                var lastCardNumber = GetLastCardNumber(_networkFolderPath);
                newCardNumber = lastCardNumber + 1;
            }
            else
            {
                if (IsCardNumberExists(_networkFolderPath, cardNumber))
                {
                    newCardNumber = int.Parse(cardNumber);
                }
                else
                {
                    newCardNumber = int.Parse(cardNumber);
                }
            }

            var formattedCardNumber = newCardNumber.ToString();
            var backgroundPath = Path.Combine(_env.WebRootPath, "cards", "karta_a.jpg");

            if (!System.IO.File.Exists(backgroundPath))
            {
                ViewBag.ErrorMessage = "T≥o karty nie zosta≥o znalezione.";
                return View("Index");
            }

            try
            {
                using (var photoStream = new MemoryStream())
                using (var backgroundStream = new FileStream(backgroundPath, FileMode.Open, FileAccess.Read))
                {
                    photo.CopyTo(photoStream);
                    photoStream.Seek(0, SeekOrigin.Begin);

                    var img = System.Drawing.Image.FromStream(photoStream);
                    var background = System.Drawing.Image.FromStream(backgroundStream);

                    var card = CreateResidentCard(background, img, firstName, lastName, formattedCardNumber);

                    var cardFileName = $"{formattedCardNumber}_{firstName} {lastName}.jpg";
                    var cardPath = Path.Combine(_networkFolderPath, cardFileName);

                    // Ensure network directory exists
                    if (!Directory.Exists(_networkFolderPath))
                    {
                        Directory.CreateDirectory(_networkFolderPath);
                    }

                    // Zapisz nowπ kartÍ, nadpisujπc istniejπcπ, jeúli to konieczne
                    card.Save(cardPath, ImageFormat.Jpeg);

                    // Copy to local directory
                    var localCardPath = Path.Combine(_localFolderPath, cardFileName);
                    if (System.IO.File.Exists(localCardPath))
                    {
                        System.IO.File.Delete(localCardPath);
                    }
                    System.IO.File.Copy(cardPath, localCardPath);

                    ViewBag.CardUrl = $"/cards/{cardFileName}";

                    // Dodaj nowy wiersz do pliku Excel
                    AddCardInfoToExcel(formattedCardNumber, firstName, lastName, startDate, endDate);
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Wystπpi≥ b≥πd: {ex.Message}";
                return View("Index");
            }

            return View("Index");
        }

        private int GetLastCardNumber(string folderPath)
        {
            var files = Directory.GetFiles(folderPath, "*.jpg");
            var lastNumber = 0;

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var parts = fileName.Split('_');

                if (parts.Length > 1 && int.TryParse(parts[0], out var number))
                {
                    if (number > lastNumber)
                    {
                        lastNumber = number;
                    }
                }
            }

            return lastNumber;
        }

        private bool IsCardNumberExists(string folderPath, string cardNumber)
        {
            var files = Directory.GetFiles(folderPath, "*.jpg");
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var parts = fileName.Split('_');

                if (parts.Length > 1 && parts[0] == cardNumber)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsValidPhoto(IFormFile photo)
        {
            var fileExtension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            return AllowedExtensions.Contains(fileExtension) && photo.Length <= MaxPhotoSize;
        }

        private System.Drawing.Image CreateResidentCard(System.Drawing.Image background, System.Drawing.Image photo, string firstName, string lastName, string cardNumber)
        {
            var cardWidth = background.Width;
            var cardHeight = background.Height;
            var card = new Bitmap(cardWidth, cardHeight);

            using (var g = Graphics.FromImage(card))
            {
                g.DrawImage(background, 0, 0, cardWidth, cardHeight);

                var photoWidth = 280;
                var aspectRatio = (double)photo.Width / photo.Height;
                var photoHeight = (int)(photoWidth / aspectRatio);

                var photoRect = new Rectangle(100, 160, photoWidth, photoHeight);
                g.DrawImage(photo, photoRect);

                var fontSize = 30;
                var font = new Font("Arial", fontSize);

                DrawText(g, firstName, font, new SolidBrush(Color.FromArgb(32, 55, 49)), new PointF(photoRect.Right + 20, photoRect.Bottom - MeasureTextSize(g, firstName, font).Height - MeasureTextSize(g, lastName, font).Height - 60));
                DrawText(g, lastName, font, new SolidBrush(Color.FromArgb(32, 55, 49)), new PointF(photoRect.Right + 20, photoRect.Bottom - MeasureTextSize(g, lastName, font).Height - 60));

                var cardNumberFont = new Font("Arial", fontSize);
                var cardNumberColor = ColorTranslator.FromHtml("#e2deaf");
                var formattedCardNumber = $"NUMER KARTY: {cardNumber}";

                var cardNumberY = photoRect.Bottom - MeasureTextSize(g, lastName, font).Height - 60 - -60;
                DrawText(g, formattedCardNumber, cardNumberFont, new SolidBrush(cardNumberColor), new PointF(photoRect.Right + 20, cardNumberY));

                var currentDate = DateTime.Now;
                var expirationDate = new DateTime(currentDate.Year + 2, currentDate.Month, 1).AddMonths(1).AddDays(-1);
                var formattedExpirationDate = $"{expirationDate.Month:D2}/{expirationDate.Year}";

                var validityText = $"WAØNA DO: {formattedExpirationDate}";
                var validityTextY = cardNumberY + fontSize + 20;
                DrawText(g, validityText, cardNumberFont, new SolidBrush(cardNumberColor), new PointF(photoRect.Right + 20, validityTextY));
            }

            return card;
        }

        private SizeF MeasureTextSize(Graphics g, string text, Font font)
        {
            return g.MeasureString(text, font);
        }

        private void DrawText(Graphics g, string text, Font font, Brush brush, PointF position)
        {
            g.DrawString(text, font, brush, position);
        }

        private void AddCardInfoToExcel(string cardNumber, string firstName, string lastName, DateTime? startDate, DateTime? endDate)
        {
            FileInfo fileInfo = new FileInfo(_excelFilePath);
            using (var package = new ExcelPackage(fileInfo))
            {
                var worksheet = package.Workbook.Worksheets.Count > 0
                    ? package.Workbook.Worksheets[0]
                    : package.Workbook.Worksheets.Add("Sheet1");

                // Find the first empty row
                int row = worksheet.Dimension?.End.Row + 1 ?? 1;

                // Sprawdü, czy numer karty juø istnieje w arkuszu
                var existingRow = FindRowByCardNumber(worksheet, cardNumber);
                if (existingRow > 0)
                {
                    // Nadpisz istniejπcy wiersz
                    row = existingRow;
                }

                worksheet.Cells[row, 1].Value = cardNumber;
                worksheet.Cells[row, 2].Value = $"{firstName} {lastName}";
                worksheet.Cells[row, 3].Value = startDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"); // Default to current date
                worksheet.Cells[row, 4].Value = endDate?.ToString("yyyy-MM-dd") ?? string.Empty;

                package.Save();
            }
        }
        [HttpPost]
        public IActionResult EditCardInfo(string cardNumber, string firstName = null, string lastName = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (string.IsNullOrEmpty(cardNumber))
            {
                ViewBag.ErrorMessage = "Numer karty jest wymagany do modyfikacji.";
                return View("Index");
            }

            try
            {
                FileInfo fileInfo = new FileInfo(_excelFilePath);
                using (var package = new ExcelPackage(fileInfo))
                {
                    var worksheet = package.Workbook.Worksheets.Count > 0
                        ? package.Workbook.Worksheets[0]
                        : null;

                    if (worksheet == null)
                    {
                        ViewBag.ErrorMessage = "Nie znaleziono arkusza Excel.";
                        return View("Index");
                    }

                    // Znajdü wiersz na podstawie numeru karty
                    var row = FindRowByCardNumber(worksheet, cardNumber);
                    if (row < 0)
                    {
                        ViewBag.ErrorMessage = "Numer karty nie zosta≥ znaleziony.";
                        return View("Index");
                    }

                    // Aktualizuj tylko uzupe≥nione pola
                    if (!string.IsNullOrEmpty(firstName))
                    {
                        worksheet.Cells[row, 2].Value = $"{firstName.ToUpperInvariant()} {worksheet.Cells[row, 2].Text.Split(' ')[1]}";
                    }

                    if (!string.IsNullOrEmpty(lastName))
                    {
                        worksheet.Cells[row, 2].Value = $"{worksheet.Cells[row, 2].Text.Split(' ')[0]} {lastName.ToUpperInvariant()}";
                    }

                    if (startDate.HasValue)
                    {
                        worksheet.Cells[row, 3].Value = startDate.Value.ToString("yyyy-MM-dd");
                    }

                    if (endDate.HasValue)
                    {
                        worksheet.Cells[row, 4].Value = endDate.Value.ToString("yyyy-MM-dd");
                    }

                    package.Save();
                }

                ViewBag.SuccessMessage = "Dane karty zosta≥y pomyúlnie zaktualizowane.";
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Wystπpi≥ b≥πd: {ex.Message}";
                return View("Index");
            }

            return View("Index");
        }
        private int GetLastUsedCardNumber()
        {
            var lastCardNumber = GetLastCardNumber(_networkFolderPath);
            return lastCardNumber;
        }



        private int FindRowByCardNumber(ExcelWorksheet worksheet, string cardNumber)
        {
            var rowCount = worksheet.Dimension?.End.Row ?? 0;
            for (int row = 1; row <= rowCount; row++)
            {
                if (worksheet.Cells[row, 1].Text == cardNumber)
                {
                    return row;
                }
            }
            return -1; // Numer karty nie znaleziony
        }
    }

}
