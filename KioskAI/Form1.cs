using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using Ghostscript.NET.Rasterizer;
using QRCoder;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.Windows.Compatibility;

namespace KioskAI
{
    public partial class Form1 : Form
    {
        public float FontSize { get; set; } = 30f;  // 기본 글자 크기
        PrintDocument printDoc = new PrintDocument();
        private Pen gridPen = new Pen(Color.Gray, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
        private Brush blackBrush = Brushes.Black;
        private int boxSize = 40;
        private int spacing = 20;
        private int lineSpacing = 120;
        private int currentMenuIndex = 0;  // 페이지 간 진행 상태 추적

        private string[] menus = new[] { "햄버거 세트", "피자 세트", "감자튀김 세트" };
        // PrintDialog 생성
        PrintDialog printDialog;
        public Form1()
        {
            InitializeComponent();

        }

        private void button1_Click(object sender, EventArgs e)
        {
            printDoc.PrintPage += printDocument1_PrintPage;
            printDoc.Print();
        }

        private void printDocument1_PrintPage(object sender, PrintPageEventArgs e)
        {
            Font font = new Font("Arial", FontSize);
            int startX = 100;
            int startY = 150;
            int availableHeight = e.MarginBounds.Bottom - startY;

            int linesPerPage = availableHeight / lineSpacing;
            int printedCount = 0;

            for (; currentMenuIndex < menus.Length; currentMenuIndex++)
            {
                if (printedCount >= linesPerPage)
                {
                    e.HasMorePages = true; // 다음 페이지 필요
                    break;
                }

                int lineY = startY + printedCount * lineSpacing;

                // [1] 수기 숫자 칸
                int[] boxXs = {
                    startX,
                    startX + boxSize + spacing,
                    startX + (boxSize + spacing) * 2
                };

                for (int j = 0; j < 3; j++)
                    e.Graphics.DrawRectangle(gridPen, new Rectangle(boxXs[j], lineY, boxSize, boxSize));

                // [2] 자리수 QR 코드 (수기 숫자 칸 위에 배치)
                string[] digitLabels = { "100", "10", "1" };  // 왼 → 오 (백, 십, 일)
                float digitQRScale = 1.3f;
                int digitQRSize = (int)(boxSize * digitQRScale);

                for (int j = 0; j < 3; j++)
                {
                    string digitData = (currentMenuIndex + 1).ToString() + "-" + digitLabels[j];
                    Bitmap digitQR = GenerateQRCode(digitData);

                    // ✅ 점선 네모 중심에 맞추기 위해 중심 정렬
                    int boxCenterX = boxXs[j] + boxSize / 2;
                    int qrX1 = boxCenterX - digitQRSize / 2;

                    // ✅ Y 좌표도 동일하게 정렬
                    int qrYOffset1 = (digitQRSize - boxSize) / 2;
                    int qrY1 = lineY - boxSize - 10 - qrYOffset1;

                    e.Graphics.DrawImage(digitQR, new Rectangle(qrX1, qrY1, digitQRSize, digitQRSize));
                }


                // [3] QR 코드
                string qrData = $"{"m" + (currentMenuIndex + 1).ToString()}";
                Bitmap qrImage = GenerateQRCode(qrData);
                float scaleFactor = 1.3f;
                int qrRenderSize = (int)(boxSize * scaleFactor);
                int qrX = boxXs[2] + boxSize + spacing * 2;
                int qrYOffset = (qrRenderSize - boxSize) / 2;
                int qrY = lineY - qrYOffset;
                e.Graphics.DrawImage(qrImage, new Rectangle(qrX, qrY, qrRenderSize, qrRenderSize));

                // [4] 메뉴명
                int textX = qrX + qrRenderSize + spacing;
                int pageRightMargin = e.MarginBounds.Right;
                int maxTextWidth = pageRightMargin - textX;

                float fontHeight = font.GetHeight(e.Graphics);
                float textY = lineY + (boxSize - fontHeight * 2) / 2;
                float maxTextHeight = fontHeight * 2 + 4;

                RectangleF textRect = new RectangleF(textX, textY, maxTextWidth, maxTextHeight);
                StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };

                e.Graphics.DrawString(menus[currentMenuIndex], font, blackBrush, textRect, format);

                printedCount++;
            }

            // 모든 메뉴를 출력했으면 종료
            if (currentMenuIndex >= menus.Length)
            {
                e.HasMorePages = false;
                currentMenuIndex = 0;  // 상태 초기화
            }

            font.Dispose();
        }

        private Bitmap GenerateQRCode(string data)
        {
            QRCodeGenerator qrGen = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGen.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            QRCode qrCode = new QRCode(qrCodeData);
            return qrCode.GetGraphic(20);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            printDialog = new PrintDialog
            {
                Document = printDoc,      // 인쇄할 문서 지정
                AllowSomePages = true,    // 일부 페이지만 선택 가능
                AllowSelection = true,    // 선택한 부분만 인쇄 가능
                UseEXDialog = true        // Windows 스타일의 대화상자 사용
            };
            printDialog.ShowDialog();
        }


        private void button3_Click(object sender, EventArgs e)
        {
            string pdfPath = "output.pdf";
            string imagePath = "page.png";

            // Step 1: PDF -> Bitmap using Ghostscript.NET
            ConvertPdfToImage(pdfPath, imagePath);
            Bitmap bitmap = new Bitmap(imagePath);
            var qrData = ExtractQrCodesWithSize(bitmap);

            var xTable = new Dictionary<int, Dictionary<int, float>>(); // line -> digitLevel -> x
            var yTable = new Dictionary<int, float>(); // line -> y
            var menuMap = new Dictionary<int, string>
    {
        { 1, "햄버거세트" },
        { 2, "피자세트" },
        { 3, "감자튀김세트" }
    };
            Bitmap debugBitmap = new Bitmap(bitmap);
            Graphics g = Graphics.FromImage(debugBitmap);
            Pen roiPen = new Pen(Color.Red, 2);

            Dictionary<(int menuNum, int level), SizeF> xQrSizes = new();
            Dictionary<int, SizeF> yQrSizes = new();

            foreach (var (text, point, size) in qrData)
            {
                if (text.Contains("-"))
                {
                    var parts = text.Split('-');
                    if (int.TryParse(parts[0], out int line) && int.TryParse(parts[1], out int digitLevel))
                    {
                        if (!xTable.ContainsKey(line))
                            xTable[line] = new Dictionary<int, float>();
                        xTable[line][digitLevel] = point.X;
                        xQrSizes[(line, digitLevel)] = size;
                    }
                }
                else if (text.StartsWith("m"))
                {
                    if (int.TryParse(text.Substring(1), out int menuNum))
                    {
                        yTable[menuNum] = point.Y;
                        yQrSizes[menuNum] = size;
                    }
                }
            }

            // Step 3: 각 ROI 시각화
            foreach (var menuEntry in yTable)
            {
                int menuNum = menuEntry.Key;
                float y = menuEntry.Value;

                if (!menuMap.ContainsKey(menuNum)) continue;

                if (xTable.ContainsKey(menuNum))
                {
                    foreach (int level in new[] { 100, 10, 1 })
                    {
                        if (xTable[menuNum].ContainsKey(level))
                        {
                            float x = xTable[menuNum][level];

                            float qrWidth = xQrSizes.TryGetValue((menuNum, level), out var sizeX) ? sizeX.Width : 40;
                            float qrHeight = yQrSizes.TryGetValue(menuNum, out var sizeY) ? sizeY.Height : 40;

                            Rectangle roi = new Rectangle(
                                (int)(x - qrWidth),
                                (int)(y - qrHeight),
                                (int)qrWidth * 2,
                                (int)qrHeight * 2
                            );

                            g.DrawRectangle(roiPen, roi);
                            g.DrawString($"{menuNum}-{level}", new Font("Arial", 10), Brushes.Red, roi.Location);
                        }
                    }
                }
            }

            // 저장
            debugBitmap.Save("debug_output.png", System.Drawing.Imaging.ImageFormat.Png);
            MessageBox.Show("ROI가 표시된 이미지가 저장되었습니다.");



            foreach (var menuEntry in yTable)
            {
                int menuNum = menuEntry.Key;
                float y = menuEntry.Value;

                if (!menuMap.ContainsKey(menuNum)) continue;

                string orderCount = "";
                if (xTable.ContainsKey(menuNum))
                {
                    foreach (int level in new[] { 100, 10, 1 })
                    {
                        if (xTable[menuNum].ContainsKey(level))
                        {
                            float x = xTable[menuNum][level];
                            float qrWidth = xQrSizes.TryGetValue((menuNum, level), out var xSize) ? xSize.Width : 40;
                            float qrHeight = yQrSizes.TryGetValue(menuNum, out var ySize) ? ySize.Height : 40;

                            Rectangle roi = new Rectangle(
                                (int)(x - qrWidth),
                                (int)(y - qrHeight),
                                (int)qrWidth * 2,
                                (int)qrHeight * 2
                            );

                            string digit = OCRDigit(bitmap, roi);
                            MessageBox.Show($"OCR result at ({x}, {y}) = '{digit}'");
                            orderCount += digit;
                        }
                    }
                }

                orderCount = string.IsNullOrEmpty(orderCount) ? "0" : orderCount;
                MessageBox.Show($"{menuMap[menuNum]}를 {orderCount}개 주문했습니다.");
            }
            bitmap.Dispose();
        }

        public static void ConvertPdfToImage(string inputPdf, string outputPng)
        {

            if (File.Exists(outputPng))
                File.Delete(outputPng); // ⚠ 기존 파일 삭제
            using (var rasterizer = new Ghostscript.NET.Rasterizer.GhostscriptRasterizer())
            {
                rasterizer.Open(inputPdf);
                var img = rasterizer.GetPage(300, 1);
                img.Save(outputPng, System.Drawing.Imaging.ImageFormat.Png);
                img.Dispose(); // ⭐ 명시적으로 해제
            }
        }

        public static List<(string text, PointF point, SizeF size)> ExtractQrCodesWithSize(Bitmap bitmap)
        {
            var results = new List<(string, PointF, SizeF)>();
            var reader = new ZXing.BarcodeReaderGeneric();
            var resultArray = reader.DecodeMultiple(bitmap);

            if (resultArray != null)
            {
                foreach (var r in resultArray)
                {
                    if (r.ResultPoints.Length >= 2)
                    {
                        var pt1 = r.ResultPoints[0];
                        var pt2 = r.ResultPoints[2];
                        float centerX = (pt1.X + pt2.X) / 2;
                        float centerY = (pt1.Y + pt2.Y) / 2;
                        float width = Math.Abs(pt1.X - pt2.X);
                        float height = Math.Abs(pt1.Y - pt2.Y);

                        results.Add((r.Text.Trim(), new PointF(centerX, centerY), new SizeF(width, height)));
                    }
                }
            }

            return results;
        }
        static int i = 0;
        static string OCRDigit(Bitmap bitmap, Rectangle roi)
        {
            

            //using (var engine = new TesseractEngine(@".\tessdata", "eng", EngineMode.Default))
            using (var memoryStream = new MemoryStream())
            {
                //engine.SetVariable("tessedit_char_whitelist", "0123456789");
                // 1. ROI 자르기
                Bitmap cropped = bitmap.Clone(roi, bitmap.PixelFormat);

                // 2. 명도 조절을 위한 전처리 (선택적)
                Bitmap preprocessed = PreprocessImage(cropped);
                //preprocessed.Save(i.ToString() + ".png", System.Drawing.Imaging.ImageFormat.Png);

                // 3. OCR 수행
                preprocessed.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                memoryStream.Position = 0;
                try
                {
                    var imageBytes = memoryStream.ToArray();
                    MNIST.ModelInput sampleData = new MNIST.ModelInput()
                    {
                        ImageSource = imageBytes,
                    };
                    var sortedScoresWithLabel = MNIST.PredictAllLabels(sampleData);
                    return sortedScoresWithLabel.First().Key == "NaN" ? "" : sortedScoresWithLabel.First().Key; // 가장 높은 확률의 라벨 반환
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"OCR Error: {ex.Message}");
                    return "";
                }
            }
        }

        static Bitmap PreprocessImage(Bitmap input)
        {
            Bitmap result = new Bitmap(input.Width, input.Height);
            for (int y = 0; y < input.Height; y++)
            {
                for (int x = 0; x < input.Width; x++)
                {
                    Color c = input.GetPixel(x, y);
                    int gray = (c.R + c.G + c.B) / 3;
                    Color newColor = gray > 150 ? Color.White : Color.Black;
                    result.SetPixel(x, y, newColor);
                }
            }
            return result;
        }
    }
}
