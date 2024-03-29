﻿using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MaizeUI.Things
{
    public class Helpers
    {
        private static readonly Random rng = new Random();
        private const int RandomChanceThreshold = 32;
        private const string ImageExtension = "*.png";

        public static void ResizeImage(string filePath)
        {
            using (Image image = Image.Load(filePath))
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(600, 600),
                    Sampler = new NearestNeighborResampler()
                }));
                image.Save(filePath);
            }
        }
        public static void SaveResizedSprite(Image<Rgba32> original, int multiplier, string outputFileName, string baseDirectory)
        {
            var newWidth = original.Width * multiplier;
            var newHeight = original.Height * multiplier;

            using (var resizedSprite = original.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(newWidth, newHeight),
                Mode = ResizeMode.Max,
                Sampler = new NearestNeighborResampler()
            })))
            {
                string outputPath = Path.Combine(baseDirectory, outputFileName);
                resizedSprite.Save(outputPath);
            }
        }
        public static string RemoveTextAfterEquals(string input)
        {
            int indexOfEquals = input.IndexOf('=');
            if (indexOfEquals != -1)
            {
                return input.Substring(0, indexOfEquals);
            }
            return input;
        }
        public static Rgba32 GetDominantColor(string filePath)
        {
            using (Image<Rgba32> image = (Image<Rgba32>)Image.Load(filePath))
            {
                return image[0, 0];
            }
        }
        public static int GetIterationNumberFromFilePath(string spriteFilePath)
        {
            return int.Parse(Regex.Match(spriteFilePath, @"Iteration_(\d+)").Groups[1].Value);
        }
        public static string HashDictionary(Dictionary<string, string> dict)
        {
            // Sort the dictionary by keys to ensure consistency
            var sortedDict = dict.OrderBy(kvp => kvp.Key);

            StringBuilder sb = new StringBuilder();
            foreach (var kvp in sortedDict)
            {
                sb.Append($"{kvp.Key}:{kvp.Value},");
            }

            // Create a SHA256 hash of the string
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));

                StringBuilder hashBuilder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    hashBuilder.Append(bytes[i].ToString("x2"));
                }
                return hashBuilder.ToString();
            }
        }
        public static List<string> SelectSpritesFromDirectory(string directory)
        {
            List<string> selectedSprites = new List<string>();
            var subDirectories = Directory.GetDirectories(directory).ToList();

            selectedSprites.AddRange(subDirectories.Count == 3 ?
                HandleThreeSubDirectories(subDirectories) :
                HandleGeneralCase(directory));

            return selectedSprites;
        }
        private static List<string> HandleThreeSubDirectories(List<string> subDirectories)
        {
            List<string> selected = new List<string>();
            var pairedDirectories = subDirectories.Where(dir => dir.EndsWith("&")).ToList();
            var standaloneDirectory = subDirectories.Except(pairedDirectories).FirstOrDefault();

            if (rng.Next(100) <= RandomChanceThreshold && standaloneDirectory != null)
            {
                selected.AddRange(SelectRandomFromDirectory(standaloneDirectory));
            }
            else if (pairedDirectories.Count == 2)
            {
                selected.AddRange(SelectRandomFromDirectory(pairedDirectories[0]));
                selected.AddRange(SelectRandomFromDirectory(pairedDirectories[1]));
            }

            return selected;
        }
        private static List<string> HandleGeneralCase(string directory)
        {
            return SelectRandomFromDirectory(directory);
        }
        private static List<string> SelectRandomFromDirectory(string directory)
        {
            List<string> selected = new List<string>();
            var allFiles = Directory.GetFiles(directory, ImageExtension)
                                     .Where(file => !Path.GetFileName(file).Contains("="))
                                     .ToArray();

            if (allFiles.Length > 0)
            {
                int randomIndex = rng.Next(allFiles.Length);
                if (randomIndex >= 0 && randomIndex < allFiles.Length)
                {
                    selected.Add(allFiles[randomIndex]);
                }
            }

            return selected;
        }
    }
}
