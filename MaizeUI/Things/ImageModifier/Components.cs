﻿using CsvHelper.Configuration;
using CsvHelper;
using Newtonsoft.Json;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System.Text.RegularExpressions;
using MaizeUI.ViewModels;
using System.Globalization;
using Maize;
using Maize.Models.Responses;
using System.IO.Compression;

namespace MaizeUI.Things
{
    public class Components
    {
        public static HashSet<string> previousHashes = new HashSet<string>(); // Consider persisting this.
        public static List<string> selectedSprites = new List<string>();

        public static void CreateMetadataJsonForSprite(string? metadataFilePath, string spriteFilePath, List<string> orderedSprites, int royaltyPercentage, string nftName, string nftDescription)
        {
            var properties = ExtractPropertiesFromSprites(orderedSprites);

            var attributes = properties.Select(p => new { trait_type = p.Key, value = p.Value }).ToArray();

            var nftPropertiesHash = Helpers.HashDictionary(properties);

            // Create metadata object
            var metadata = new
            {
                image = "ipfs://IMAGE_PLACEHOLDER",
                animation_url = "ipfs://ANIMATION_PLACEHOLDER",
                name = nftName,
                royalty_percentage = royaltyPercentage,
                description = nftDescription,
                collection_metadata = $"https://nftinfos.loopring.io/COLLECTION_PLACEHOLDER",
                mint_channel = "Maize",
                properties,
                attributes
            };

            // Serialize to JSON and write to file
            string jsonOutput = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            string jsonFilePath = Path.ChangeExtension(spriteFilePath, ".json");
            File.WriteAllText(jsonFilePath, jsonOutput);
            if (metadataFilePath != null)
            {
                jsonFilePath = Path.ChangeExtension(metadataFilePath, ".json");
                File.WriteAllText(jsonFilePath, jsonOutput);
            }
        }
        public static bool CheckForDuplicates(List<string> orderedSprites)
        {
            var currentHash = Helpers.HashDictionary(ExtractPropertiesFromSprites(orderedSprites, true));
            if (previousHashes.Contains(currentHash)) return false;

            previousHashes.Add(currentHash);
            return true;
        }
        public static Dictionary<string, string> ExtractPropertiesFromSprites(List<string> orderedSprites, bool excludeMarkedDirectories = false)
        {
            var properties = new Dictionary<string, string>();

            foreach (var spritePath in orderedSprites)
            {
                var directoryName = new DirectoryInfo(Path.GetDirectoryName(spritePath)).Name;
                if (excludeMarkedDirectories && Regex.IsMatch(directoryName, @"^\d+!_"))
                {
                    continue;
                }
                directoryName = Regex.Replace(directoryName, @"^\d+!?|&$", "").Trim();
                directoryName = directoryName.Replace("_", " ").Trim();

                var spriteName = Path.GetFileNameWithoutExtension(spritePath);
                spriteName = Regex.Replace(spriteName, @"^X#\d{1,2}", "");

                var propertyParts = spriteName.Split(new char[] { '-', '+' });
                for (int i = 0; i < propertyParts.Length; i++)
                {
                    int indexOfEquals = propertyParts[i].IndexOf('=');
                    if (indexOfEquals != -1)
                    {
                        propertyParts[i] = propertyParts[i].Substring(0, indexOfEquals);
                    }
                }

                if (propertyParts.Length >= 3) // Expecting: [property, value, remainder]
                {
                    var propertyName = propertyParts[0].Trim().Replace("_", " ");
                    var propertyValue = propertyParts[1].Trim().Replace("_", " ");
                    var remainder = propertyParts[2].Trim().Replace("_", " ");
                    properties[directoryName] = propertyName;
                    properties[propertyValue] = remainder;
                }
                else if (propertyParts.Length == 2) // Expecting: [property, value]
                {
                    var propertyName = propertyParts[0].Trim().Replace("_", " ");
                    var propertyValue = propertyParts[1].Trim().Replace("_", " ");
                    properties[propertyName] = propertyValue;
                }
                else
                {
                    spriteName = spriteName.Replace("_", " ");
                    properties[directoryName] = spriteName;
                }
            }

            return properties;
        }


        public static List<string> StackRandomSpritesFromSubdirectories(string baseDirectory)
        {
            List<string> localSelectedSprites = new List<string>(); // Create a new list here

            // Get all directories in the base directory
            var allDirectories = Directory.GetDirectories(baseDirectory).ToList();

            foreach (var directory in allDirectories)
            {
                localSelectedSprites.AddRange(Helpers.SelectSpritesFromDirectory(directory)); // Use local list
            }

            // Replace sprites based on relationships after all have been selected
            var relationships = ExtractRelationshipsFromDirectory(baseDirectory);
            ReplaceSpritesBasedOnRelationships(localSelectedSprites, relationships); // Use local list

            return localSelectedSprites.OrderBy(sprite =>
            {
                var dirInfo = new DirectoryInfo(Path.GetDirectoryName(sprite));

                // Adjust the regex to capture a number possibly followed by '!'
                while (!Regex.IsMatch(dirInfo.Name, @"^\d+!?_") && dirInfo.Parent != null)
                {
                    dirInfo = dirInfo.Parent;
                }

                string[] parts = dirInfo.Name.Split('_');
                int number = 0;

                // Use regex to extract the number without '!'
                if (parts.Length >= 2 && int.TryParse(Regex.Match(parts[0], @"\d+").Value, out number))
                {
                    return number;
                }

                return int.MaxValue;
            })
            .ThenBy(sprite =>
            {
                // Order by the immediate parent directory name alphabetically
                var parentDir = new DirectoryInfo(Path.GetDirectoryName(sprite));
                return parentDir.Name;
            })
            .ToList();
        }

        public static void ProcessSprites(string nftDirectory, List<string> orderedSprites, string iterationDirectory, string bulkUploadDirectory, string selectedItem)
        {
            if (orderedSprites.Count > 0)
            {
                using (var firstSprite = Image.Load(orderedSprites[0]))
                {
                    using (var stackedSprite = new Image<Rgba32>(firstSprite.Width, firstSprite.Height))
                    {
                        var iterationNumber = Helpers.GetIterationNumberFromFilePath(iterationDirectory);

                        foreach (var spritePath in orderedSprites)
                        {
                            if (!spritePath.Contains("background") && !spritePath.Contains("bobber"))
                            {
                                using (var sprite = Image.Load(spritePath))
                                {
                                    stackedSprite.Mutate(ctx => ctx.DrawImage(sprite, new Point(0, 0), 1f));
                                }
                            }
                        }

                        string outputFileName = $"sprite_{iterationNumber}_1x.png";
                        var outputPath = Path.Combine(iterationDirectory, outputFileName);
                        stackedSprite.Save(outputPath);
                        outputFileName = $"{iterationNumber}.png";
                        outputPath = Path.Combine($"{bulkUploadDirectory}\\1", outputFileName);
                        stackedSprite.Save(outputPath);

                        Helpers.SaveResizedSprite(stackedSprite, 2, $"sprite_{iterationNumber}_2x.png", iterationDirectory);
                        Helpers.SaveResizedSprite(stackedSprite, 2, $"{iterationNumber}.png", $"{bulkUploadDirectory}\\2");
                        Helpers.SaveResizedSprite(stackedSprite, 3, $"sprite_{iterationNumber}_3x.png", iterationDirectory);
                        Helpers.SaveResizedSprite(stackedSprite, 3, $"{iterationNumber}.png", $"{bulkUploadDirectory}\\3");


                        var backgroundColor = Helpers.GetDominantColor(orderedSprites.First());

                        if (selectedItem.Contains("Looper"))
                            CreatePFPImageFromSprite(nftDirectory, stackedSprite, backgroundColor, iterationDirectory, bulkUploadDirectory);
                        else if(selectedItem.Contains("Weapon"))
                        {
                            CreateWeaponItem(stackedSprite, iterationDirectory, iterationNumber.ToString(), bulkUploadDirectory);
                            CreatePFPImageFromSpriteWeapon(stackedSprite, backgroundColor, nftDirectory, iterationNumber, bulkUploadDirectory);
                        }
                        else if (selectedItem.Contains("Fishing Rod"))
                        {
                            string targetFileName = "bobber";

                            string filePathContainingBobber = orderedSprites.FirstOrDefault(filePath => filePath.Contains(targetFileName, StringComparison.OrdinalIgnoreCase));
                            CreateFishingItem(filePathContainingBobber, iterationDirectory, iterationNumber.ToString(), bulkUploadDirectory);
                            CreatePFPImageFromSpriteFishing(filePathContainingBobber, stackedSprite, backgroundColor, nftDirectory, iterationNumber);
                        }
                    }
                }
            }
        }
        private static void CreatePFPImageFromSpriteFishing(string bobber, Image<Rgba32> sprite, Rgba32 backgroundColor, string baseDirectory, int iterationNumber)
        {
            int sectionWidth = 30;
            int sectionHeight = 30;

            using (var pfpImage = new Image<Rgba32>(27, 27, backgroundColor))
            {
                // Adjusted coordinates for top-left corner to start cropping
                int startX = 16; // moved 1 pixel to the left
                int startY = 61; // moved 2 pixels up

                // Crop the section from the sprite
                var spriteSection = sprite.Clone(ctx => ctx.Crop(new Rectangle(startX, startY, sectionWidth, sectionHeight)));

                // Load the bobber image
                using (var bobberImage = Image.Load<Rgba32>(bobber))
                {
                    // Crop the desired section from the bobber image
                    var bobberSection = bobberImage.Clone(ctx => ctx.Crop(new Rectangle(0, 0, 15, 15)));

                    // Overlay the cropped bobber section onto the pfpImage
                    pfpImage.Mutate(ctx =>
                    {
                        ctx.DrawImage(spriteSection, new Point(0, 0), 1f);
                        ctx.DrawImage(bobberSection, new Point(0, 0), 1f); // Adjust the position as needed
                    });
                }

                string pfpFileName = $"{iterationNumber}.png";
                string pfpPath = Path.Combine(baseDirectory, pfpFileName);

                pfpImage.Save(pfpPath);
                ResizeImage(pfpPath);
            }
        }
        private static void CreateFishingItem(string bobber, string baseDirectory, string iterationNumber, string bulkUploadDirectory)
        {
            // Load the bobber image
            using (var bobberImage = Image.Load<Rgba32>(bobber))
            {
                if (baseDirectory != null)
                {
                    // Create a copy of the bobber image
                    var utilityImage = bobberImage.Clone();

                    // Save the modified image with the new name
                    string utilityFileName = $"item_{iterationNumber}.png";
                    string utilityPath = Path.Combine(baseDirectory, utilityFileName);
                    utilityImage.Save(utilityPath);

                    utilityPath = Path.Combine($"{bulkUploadDirectory}\\4", utilityFileName.Remove(0, 5));
                    utilityImage.Save(utilityPath);

                    Helpers.SaveResizedSprite(utilityImage, 2, $"item_{iterationNumber}_2x.png", baseDirectory);
                    Helpers.SaveResizedSprite(utilityImage, 2, $"{iterationNumber}.png", $"{bulkUploadDirectory}\\5");
                    Helpers.SaveResizedSprite(utilityImage, 3, $"item_{iterationNumber}_3x.png", baseDirectory);
                    Helpers.SaveResizedSprite(utilityImage, 3, $"{iterationNumber}.png", $"{bulkUploadDirectory}\\6");
                }
                else
                {
                    var utilityImage = bobberImage.Clone();
                    string utilityFileName = $"item_{iterationNumber}.png";
                    var utilityPath = Path.Combine($"{bulkUploadDirectory}\\4", utilityFileName.Remove(0, 5));
                    utilityImage.Save(utilityPath);

                    Helpers.SaveResizedSprite(utilityImage, 2, $"{iterationNumber}.png", $"{bulkUploadDirectory}\\5");
                    Helpers.SaveResizedSprite(utilityImage, 3, $"{iterationNumber}.png", $"{bulkUploadDirectory}\\6");
                }

            }
        }
        private static void CreatePFPImageFromSpriteWeapon(Image<Rgba32> sprite, Rgba32 backgroundColor, string baseDirectory, int iterationNumber, string bulkUploadDirectory)
        {
            int sectionWidth = 30;
            int sectionHeight = 30;

            using (var pfpImage = new Image<Rgba32>(27, 27, backgroundColor))
            {
                // Adjusted coordinates for top-left corner to start cropping
                int startX = 16; // moved 1 pixel to the left
                int startY = 61; // moved 2 pixels up

                // Crop the section from the sprite
                var spriteSection = sprite.Clone(ctx => ctx.Crop(new Rectangle(startX, startY, sectionWidth, sectionHeight)));

                // Overlay the cropped sprite section onto the new image
                pfpImage.Mutate(ctx => ctx.DrawImage(spriteSection, new Point(0, 0), 1f));

                string pfpFileName = $"{iterationNumber}.png";
                string pfpPath = Path.Combine(baseDirectory, pfpFileName);

                pfpImage.Save(pfpPath);
                ResizeImage(pfpPath);
            }
        }
        private static Rectangle FindSpriteBounds(Image<Rgba32> sprite, Rectangle searchBounds)
        {
            int xMin = int.MaxValue, xMax = int.MinValue, yMin = int.MaxValue, yMax = int.MinValue;

            for (int y = searchBounds.Top; y < searchBounds.Bottom; y++)
            {
                for (int x = searchBounds.Left; x < searchBounds.Right; x++)
                {
                    Rgba32 pixel = sprite[x, y];
                    if (pixel.A > 0) // Non-transparent pixel found
                    {
                        xMin = Math.Min(xMin, x);
                        xMax = Math.Max(xMax, x);
                        yMin = Math.Min(yMin, y);
                        yMax = Math.Max(yMax, y);
                    }
                }
            }

            if (xMin <= xMax && yMin <= yMax)
            {
                return new Rectangle(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
            }
            else
            {
                throw new Exception("Sprite not found within the specified search bounds.");
            }
        }
        private static void CreateWeaponItem(Image<Rgba32> sprite, string baseDirectory, string iterationNumber, string bulkUploadDirectory)
        {
            Rectangle searchBounds = new Rectangle(0, 400, 31, 31);
            Rectangle cropRectangle = FindSpriteBounds(sprite, searchBounds);

            var spriteSection = sprite.Clone(ctx => ctx.Crop(cropRectangle));

            using (var utilityImage = new Image<Rgba32>(96, 16))
            {
                for (int i = 0; i < 6; i++)
                {
                    utilityImage.Mutate(ctx => ctx.DrawImage(spriteSection, new Point(i * 16 + 3, 3), 1f));
                }

                if (baseDirectory != null)
                {
                    string utilityFileName = $"item_{iterationNumber}.png";
                    string utilityPath = Path.Combine(baseDirectory, utilityFileName);
                    utilityImage.Save(utilityPath);

                    utilityPath = Path.Combine($"{bulkUploadDirectory}\\4", utilityFileName.Remove(0, 5));
                    utilityImage.Save(utilityPath);

                    Helpers.SaveResizedSprite(utilityImage, 2, $"item_{iterationNumber}_2x.png", baseDirectory);
                    Helpers.SaveResizedSprite(utilityImage, 2, $"{iterationNumber}.png", $"{bulkUploadDirectory}\\5");
                    Helpers.SaveResizedSprite(utilityImage, 3, $"item_{iterationNumber}_3x.png", baseDirectory);
                    Helpers.SaveResizedSprite(utilityImage, 3, $"{iterationNumber}.png", $"{bulkUploadDirectory}\\6");
                }
                else
                {
                    string utilityFileName = $"item_{iterationNumber}.png";

                    var utilityPath = Path.Combine($"{bulkUploadDirectory}\\4", utilityFileName.Remove(0, 5));
                    utilityImage.Save(utilityPath);

                    Helpers.SaveResizedSprite(utilityImage, 2, $"{iterationNumber}.png", $"{bulkUploadDirectory}\\5");
                    Helpers.SaveResizedSprite(utilityImage, 3, $"{iterationNumber}.png", $"{bulkUploadDirectory}\\6");
                }

            }
        }
        public static void ProcessLayers(int iterationNumber, List<string> orderedLayers, string nftDirectory)
        {
            if (orderedLayers.Count > 0)
            {
                using (var firstSprite = Image.Load(orderedLayers[0]))
                {
                    using (var stackedSprite = new Image<Rgba32>(firstSprite.Width, firstSprite.Height))
                    {

                        foreach (var layerPath in orderedLayers)
                        {
                            using (var sprite = Image.Load(layerPath))
                            {
                                stackedSprite.Mutate(ctx => ctx.DrawImage(sprite, new Point(0, 0), 1f));
                            }
                        }

                        string outputFileName = $"nft{iterationNumber}.png";
                        var outputPath = Path.Combine(nftDirectory, outputFileName);
                        stackedSprite.Save(outputPath);
                    }
                }
            }
        }
        public static void ProcessMetadata(string metadataFilePath, List<string> orderedSprites, string iterationDirectory, int royaltyPercentage, string nftName, string nftDescription)
        {
            if (orderedSprites.Count > 0)
            {
                using (var firstSprite = Image.Load(orderedSprites[0]))
                {
                    using (var stackedSprite = new Image<Rgba32>(firstSprite.Width, firstSprite.Height))
                    {
                        var iterationNumber = Helpers.GetIterationNumberFromFilePath(iterationDirectory);
                        string outputPath = Path.Combine(iterationDirectory, $"metadata{iterationNumber}.png");
                        string metadataOutputPath = Path.Combine(metadataFilePath, $"metadata{iterationNumber}.png");
                        CreateMetadataJsonForSprite(metadataOutputPath, outputPath, orderedSprites, royaltyPercentage, nftName, nftDescription);
                    }
                }
            }
        }
        public static void ProcessMetadataNfts(int iterationNumber, List<string> orderedSprites, string metadataDirectory, int royaltyPercentage, string nftName, string nftDescription)
        {
            if (orderedSprites.Count > 0)
            {
                using (var firstSprite = Image.Load(orderedSprites[0]))
                {
                    using (var stackedSprite = new Image<Rgba32>(firstSprite.Width, firstSprite.Height))
                    {
                        string outputPath = Path.Combine(metadataDirectory, $"metadata{iterationNumber}.png");
                        CreateMetadataJsonForSprite(null, outputPath, orderedSprites, royaltyPercentage, nftName, nftDescription);
                    }
                }
            }
        }

        private static void CreatePFPImageFromSprite(string nftDirectory, Image<Rgba32> sprite, Rgba32 backgroundColor, string baseDirectory, string bulkUploadDirectory)
        {
            int sectionWidth = 27;
            int sectionHeight = 27;

            using (var pfpImage = new Image<Rgba32>(27, 27, backgroundColor))
            {
                // Crop the section from the sprite
                var spriteSection = sprite.Clone(ctx => ctx.Crop(new Rectangle(6, 28, sectionWidth, sectionHeight)));

                // Overlay the cropped sprite section onto the new image
                pfpImage.Mutate(ctx => ctx.DrawImage(spriteSection, new Point(0, 0), 1f));

                string pfpFileName = $"{Helpers.GetIterationNumberFromFilePath(baseDirectory)}.png";
                string pfpPath = Path.Combine(baseDirectory, pfpFileName);

                pfpImage.Save(pfpPath);
                ResizeImage(pfpPath);

                pfpPath = Path.Combine(nftDirectory, pfpFileName);
                pfpImage.Save(pfpPath);
                ResizeImage(pfpPath);

                pfpPath = Path.Combine($"{bulkUploadDirectory}\\profilepic", pfpFileName);
                pfpImage.Save(pfpPath);
                ResizeImage(pfpPath);
            }
        }
        private static async Task GetNftImageForSpriteProfilePicAsync(string nftCid, string bulkUploadDirectory, string nftId)
        {
            INftMetadataService nftMetadataService = new NftMetadataService("https://ipfs.loopring.io/ipfs/");

            var image = await nftMetadataService.SaveFileFromCid(nftCid);

            string pfpPath = Path.Combine($"{bulkUploadDirectory}\\profilepic", nftId + ".png");
            File.Move(image, pfpPath);
            ResizeImage(pfpPath);
        }
        public static void ReplaceSpritesBasedOnRelationships(List<string> selectedSprites, Dictionary<string, string> relationships)
        {
            // Check relationships
            foreach (var relationship in relationships)
            {
                var mainItem = relationship.Key;
                var relatedItem = relationship.Value;

                // Check if main item and any related item is in selectedSprites
                bool hasMainItem = selectedSprites.Any(s => s.Contains(mainItem) && !s.Contains("="));
                bool hasRelatedItem = selectedSprites.Any(s => s.Contains(relatedItem) && !s.Contains("="));
                bool hasCombinedItem = selectedSprites.Any(s => s.Contains(mainItem + "=" + relatedItem));

                // If both main item and its related part is in list, but the combined item isn't, add the combined one and remove the standalone items
                if (hasMainItem && hasRelatedItem && !hasCombinedItem)
                {
                    var directoryOfMainItem = new DirectoryInfo(Path.GetDirectoryName(selectedSprites.First(s => s.Contains(mainItem) && !s.Contains("=")))).FullName;

                    var combinedSpritePath = Directory.GetFiles(directoryOfMainItem, mainItem + "=" + relatedItem + "*.png").FirstOrDefault();
                    if (combinedSpritePath != null)
                    {
                        // Add combined item
                        selectedSprites.Add(combinedSpritePath);

                        // Remove standalone items
                        selectedSprites.RemoveAll(s => s.Contains(mainItem) && !s.Contains("="));
                    }
                }
            }
        }

        public static Dictionary<string, string> ExtractRelationshipsFromDirectory(string baseDirectory)
        {
            Dictionary<string, string> relationships = new Dictionary<string, string>();

            var allFilesWithDash = Directory.GetFiles(baseDirectory, "*=*.png", SearchOption.AllDirectories);
            foreach (var file in allFilesWithDash)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var parts = fileName.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    relationships[parts[0]] = parts[1];
                }
            }

            return relationships;
        }
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
        public static async Task NftToSpriteAsync(string bulkUploadDirectory, string basePath, string csvFile, string minterAddress, string collectionAddress, 
            int royaltyPercentage, string projectName, string selectedItem)
        {
            var nftRecords = new List<NftRecord>();
            using (var reader = new StreamReader(csvFile))
            using (var csv = new CsvReader(reader, new CsvConfiguration(new CultureInfo("en-US"))))
            {
                nftRecords = csv.GetRecords<NftRecord>().ToList();
            }
            List<OutputRecord> outputRecords = new List<OutputRecord>();

            foreach (var record in nftRecords)
            {
                // 2. Parse properties JSON
                Dictionary<string, string> properties = JsonConvert.DeserializeObject<Dictionary<string, string>>(record.properties);

                // 3. Map keys to folder names and values to file names
                Dictionary<string, Image<Rgba32>> imagesToStack = new Dictionary<string, Image<Rgba32>>();
                foreach (var key in properties.Keys)
                {
                    if (key.Contains("background")) continue;

                    string folderName = key.Replace(" ", "_");

                    // Search for the directory that ends with the given folderName
                    string foundDirectory = Directory.GetDirectories(basePath, "*" + folderName).FirstOrDefault();

                    if (string.IsNullOrEmpty(foundDirectory))
                    {
                        //not found
                        continue;
                    }

                    string fileName = properties[key].Replace(" ", "_") + ".png";
                    string imagePath = Path.Combine(foundDirectory, fileName);

                    if (File.Exists(imagePath))
                    {
                        Image<Rgba32> img = Image.Load<Rgba32>(imagePath);
                        imagesToStack.Add(imagePath, img);
                    }
                }

                // 4. Generate new image by stacking
                int width = imagesToStack.Values.First().Width;
                int height = imagesToStack.Values.First().Height;

                using (Image<Rgba32> newImage = new Image<Rgba32>(width, height))
                {
                    foreach (var img in imagesToStack)
                    {
                        if (img.Key.Contains("bobber")) continue;
                        newImage.Mutate(ctx => ctx.DrawImage(img.Value, new Point(0, 0), 1f));
                    }

                    string outputFileName = $"{record.nftId}.png";
                    var outputPath = Path.Combine($"{bulkUploadDirectory}\\1", outputFileName);
                    newImage.Save(outputPath);

                    Helpers.SaveResizedSprite(newImage, 2, $"{record.nftId}.png", $"{bulkUploadDirectory}\\2");
                    Helpers.SaveResizedSprite(newImage, 3, $"{record.nftId}.png", $"{bulkUploadDirectory}\\3");

                    if (selectedItem.Contains("Looper"))
                        await GetNftImageForSpriteProfilePicAsync(record.nftCid, bulkUploadDirectory, record.nftId);
                    else if (selectedItem.Contains("Weapon"))
                    {
                        CreateWeaponItem(newImage, null, record.nftId, bulkUploadDirectory);
                    }
                    else if (selectedItem.Contains("Fishing Rod"))
                    {
                        string targetFileName = "bobber";

                        string filePathContainingBobber = imagesToStack.Keys.FirstOrDefault(filePath => filePath.Contains(targetFileName, StringComparison.OrdinalIgnoreCase));
                        CreateFishingItem(filePathContainingBobber, null, record.nftId, bulkUploadDirectory);
                    }
                    string assetType;
                    if (selectedItem.Contains("Looper"))
                        assetType = "armor";
                    else if (selectedItem.Contains("Weapon"))
                        assetType = "weapon";
                    else if (selectedItem.Contains("Fishing"))
                        assetType = "fishingrod";
                    else
                        assetType = "";
                    OutputRecord outputRecord = new OutputRecord
                    {
                        LooperName = record.name, // Assuming LooperName exists in the original CSV
                        ShortNFTID = record.nftId,
                        NFTID = $"{minterAddress}-0-{collectionAddress}-{record.nftId}-{royaltyPercentage}",
                        projectname = projectName, // Replace with actual project name
                        assetyype = assetType // Replace with actual asset type
                    };

                    outputRecords.Add(outputRecord);
                }

            }
            // Write to the new CSV file
            using (var writer = new StreamWriter($"{bulkUploadDirectory}\\ToLooperLands.csv"))
            using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                csv.WriteRecords(outputRecords);
            }
            CreateInstructionTextFile(bulkUploadDirectory + "\\ReadMeForNextSteps.txt");
            ZipFolderAndDeleteSource(bulkUploadDirectory, $"{Path.GetDirectoryName(bulkUploadDirectory)}\\ToLooperLands.zip");
            static void ZipFolderAndDeleteSource(string sourceFolder, string zipFilePath)
            {
                try
                {
                    ZipFile.CreateFromDirectory(sourceFolder, zipFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }
            static void CreateInstructionTextFile(string filePath)
            {
                // Content to be written to the text file
                string content = @"Now take your BulkUpload folder to the Looper Lands discord.

1. https://discord.gg/QRqDEqsNjT
2. Go to the channel #open-a-ticket
3. type in /open and click the /open link
4. click subject and type 'BulkUpload from <Your Wallet address>'
   example: BulkUpload from 0x834e8242ba51aed0eef95827d241f2db39f04ad9
5. Click into the created ticket
6. Give the LL team a nice message and upload the ToLooperLands.zip file. 
7. Send the 35 LRC per asset fee to looperlands.loopring.eth.
8. Done!
9. Keep an eye on your ticket ^_^";

                try
                {
                    // Write the content to the text file
                    File.WriteAllText(filePath, content);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }
        }
        private class NftRecord
        {
            public string name { get; set; }
            public string nftId { get; set; }
            public string nftCid { get; set; }
            public string properties { get; set; }
        }
        private class OutputRecord
        {
            public string LooperName { get; set; }
            public string ShortNFTID { get; set; }
            public string NFTID { get; set; }
            public string projectname { get; set; }
            public string assetyype { get; set; }
        }
    }
}
