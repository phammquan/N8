using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace SimpleLLM
{
    public class TextData
    {
        public string? Text { get; set; } // Khai báo nullable
    }

    public class TextPrediction
    {
        [ColumnName("PredictedLabel")]
        public string? PredictedText { get; set; } // Khai báo nullable
    }

    class Program
    {
        static void Main(string[] args)
        {
            var mlContext = new MLContext();

            // Đọc nội dung từ file Word
            var data = new List<TextData>();
            string filePath = "D:\\hoc_tap_neu\\CCMHĐ\\Training.docx";

            var stopwatch = new Stopwatch();

            try
            {
                stopwatch.Start();
                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(filePath, false))
                {
                    var body = wordDoc.MainDocumentPart.Document.Body;
                    foreach (var paragraph in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                    {
                        data.Add(new TextData { Text = paragraph.InnerText });
                    }
                }
                stopwatch.Stop();
                Console.WriteLine($"Time to read file: {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("File not found. Please check the file path.");
                return;
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Access denied. Please check your permissions.");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return;
            }

            stopwatch.Restart();
            var trainingData = mlContext.Data.LoadFromEnumerable(data);
            stopwatch.Stop();
            Console.WriteLine($"Time to load data: {stopwatch.ElapsedMilliseconds} ms");

            // Define the data preparation and training pipeline
            var pipeline = mlContext.Transforms.Text.NormalizeText("NormalizedText", nameof(TextData.Text))
                .Append(mlContext.Transforms.Text.TokenizeIntoWords("Tokens", "NormalizedText"))
                .Append(mlContext.Transforms.Text.RemoveDefaultStopWords("Tokens"))
                .Append(mlContext.Transforms.Conversion.MapValueToKey("Tokens", "Tokens")) // Chuyển đổi các token thành các khóa
                .Append(mlContext.Transforms.Text.ProduceNgrams("Features", "Tokens", ngramLength: 2, useAllLengths: false))
                .Append(mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(TextData.Text)))
                .Append(mlContext.Transforms.Concatenate("Features", "Features"))
                .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));

            stopwatch.Restart();
            // Train the model
            var model = pipeline.Fit(trainingData);
            stopwatch.Stop();
            Console.WriteLine($"Time to train model: {stopwatch.ElapsedMilliseconds} ms");

            // Create prediction engine
            var predictionEngine = mlContext.Model.CreatePredictionEngine<TextData, TextPrediction>(model);

            // Nhập văn bản từ bàn phím
            Console.WriteLine("Please enter a text:");
            var inputText = Console.ReadLine();

            // Kiểm tra null cho inputText
            if (string.IsNullOrEmpty(inputText))
            {
                Console.WriteLine("Input cannot be null or empty.");
                return;
            }

            // Predict the next word
            var input = new TextData { Text = inputText };
            var prediction = predictionEngine.Predict(input);

            // Hiển thị kết quả dự đoán chi tiết
            Console.WriteLine($"Input: {input.Text} \nPredicted:\n{prediction.PredictedText}");

        }
    }
}
