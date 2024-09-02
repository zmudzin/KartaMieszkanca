using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KartaMieszkanca.Controllers
{
    public class ResidentCardController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly string _networkFolderPath;
        private readonly string _localFolderPath;
        private const int MaxPhotoSize = 5 * 1024 * 1024; // Maks. 5 MB
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };

        public ResidentCardController(IWebHostEnvironment env)
        {
            _env = env;
            _networkFolderPath = @"\\10.0.0.176\Karta Mieszkanca\_PODKOWIAÑSKA KARTA MIESZKAÑCA";
            _localFolderPath = Path.Combine(_env.WebRootPath, "cards");
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult GenerateCard(IFormFile photo, string firstName, string lastName)
        {
            if (photo == null || string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
            {
                ViewBag.ErrorMessage = "Wszystkie pola s¹ wymagane.";
                return View("Index");
            }

            if (!IsValidPhoto(photo))
            {
                ViewBag.ErrorMessage = "Nieprawid³owy format lub rozmiar zdjêcia.";
                return View("Index");
            }

            // Konwersja imienia i nazwiska na wielkie litery
            firstName = firstName.ToUpperInvariant();
            lastName = lastName.ToUpperInvariant();

            var lastCardNumber = GetLastCardNumber(_networkFolderPath);
            var newCardNumber = lastCardNumber + 1;
            var cardNumber = newCardNumber.ToString();

            var backgroundPath = Path.Combine(_env.WebRootPath, "cards", "karta_a.jpg");

            if (!System.IO.File.Exists(backgroundPath))
            {
                ViewBag.ErrorMessage = "T³o karty nie zosta³o znalezione.";
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

                    var card = CreateResidentCard(background, img, firstName, lastName, cardNumber);

                    var cardFileName = $"{cardNumber}_{firstName} {lastName}.jpg";
                    var cardPath = Path.Combine(_networkFolderPath, cardFileName);

                    // Ensure network directory exists
                    if (!Directory.Exists(_networkFolderPath))
                    {
                        Directory.CreateDirectory(_networkFolderPath);
                    }

                    card.Save(cardPath, ImageFormat.Jpeg);

                    // Copy to local directory
                    var localCardPath = Path.Combine(_localFolderPath, cardFileName);
                    if (System.IO.File.Exists(localCardPath))
                    {
                        System.IO.File.Delete(localCardPath);
                    }
                    System.IO.File.Copy(cardPath, localCardPath);

                    ViewBag.CardUrl = $"/cards/{cardFileName}";
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Wyst¹pi³ b³¹d: {ex.Message}";
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

                var validityText = $"WA¯NA DO: {formattedExpirationDate}";
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
    }
}
