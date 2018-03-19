using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace DotNetThoughts.Sprite.Tool
{
    internal class App
    {
        private readonly ILogger<App> _logger;
        public App(ILogger<App> logger)
        {
            _logger = logger;
        }
        public void Run(string[] args)
        {

            var spriteImagesApp = new CommandLineApplication();
            spriteImagesApp.Name = "sprite";
            spriteImagesApp.Description = "For generating Image sprite and CSS file.";
            spriteImagesApp.HelpOption("-?|-h|--help");
            var sourceImageDirectoryOption =
             spriteImagesApp.Option("-s|--source", "Directory with images for generating sprite.", CommandOptionType.SingleValue);
            var targetDirectoryOption =
            spriteImagesApp.Option("-t|--target", "Output directory where app will generate the image and css file.", CommandOptionType.SingleValue);
            spriteImagesApp.Error = Console.Out;
            if (args.Length <= 0)
            {
                spriteImagesApp.ShowHelp();
                return;
            }
            spriteImagesApp.OnExecute(() =>
            {
                if (!sourceImageDirectoryOption.HasValue())
                {
                    _logger.LogError("Error: Source directory not specified.");
                    return -1;
                }

                if (!targetDirectoryOption.HasValue())
                {
                    _logger.LogError("Error: Target directory not specified.");
                    return -1;
                }

                if (!Directory.Exists(sourceImageDirectoryOption.Value()))
                {
                    _logger.LogError($"Error : Directory {sourceImageDirectoryOption.Value()} not found.");
                    return -1;
                };

                var files = GetImages(sourceImageDirectoryOption.Value());
                if (!files.Any())
                {
                    _logger.LogError($"Error : Couldn't find any images from the directory {sourceImageDirectoryOption.Value()}");
                    return -1;
                }

                if (!Directory.Exists(targetDirectoryOption.Value()))
                {
                    _logger.LogInformation($"Directory {targetDirectoryOption.Value()} not found. Creating it.");
                    Directory.CreateDirectory(targetDirectoryOption.Value());
                    _logger.LogInformation($"{targetDirectoryOption.Value()} Created.");
                }

                GenerateSprite(files, targetDirectoryOption.Value());
                return 0;
            });

            try
            {
                spriteImagesApp.Execute(args);
            }
            catch (CommandParsingException ex)
            {
                Console.WriteLine(ex.Message);
                _logger.LogError(ex.Message);
            }
        }

        private IEnumerable<string> GetImages(string directory)
        {
            var sourceImages = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase));

            return sourceImages;
        }

        private void GenerateSprite(IEnumerable<string> files, string targetDirectory)
        {
            var startTime = DateTime.UtcNow;
            var images = new List<Bitmap>();
            var styles = new List<Style>();
            Bitmap finalImage = null;

            try
            {
                int width = 0;
                int height = 0;

                foreach (string image in files)
                {
                    var bitmap = new Bitmap(image);
                    bitmap.Tag = image;
                    width += bitmap.Width;
                    height = bitmap.Height > height ? bitmap.Height : height;
                    images.Add(bitmap);

                    _logger.LogDebug($"Adding Image :{ Path.GetFileName(image)}.");
                }
                _logger.LogDebug($"Total Images {images.Count}");
                _logger.LogDebug("Building the final image.");
                finalImage = new Bitmap(width, height);
                using (Graphics graphics = Graphics.FromImage(finalImage))
                {
                    graphics.Clear(Color.Transparent);
                    int offset = 0;
                    foreach (var image in images)
                    {
                        graphics.DrawImage(image, new Rectangle(offset, 0, image.Width, image.Height));
                        offset += image.Width;

                        styles.Add(new Style
                        {
                            Height = image.Height,
                            Width = image.Width,
                            Name = image.Tag.ToString(),
                            Left = offset - image.Width
                        });
                    }
                }

                finalImage.Save(Path.Combine(targetDirectory, "SpriteImage.png"), ImageFormat.Png);
                _logger.LogDebug("Image generated. Starting the CSS generation.");
                CreateCSS(styles, targetDirectory);
                _logger.LogDebug("CSS generated.");
                var timeSpent = (DateTime.UtcNow - startTime).Seconds;
                _logger.LogInformation("Generated sprite image and css successfully in {0} second{1}", timeSpent, timeSpent > 1 ? "s" : "");
            }
            catch (Exception ex)
            {
                if (finalImage != null)
                {
                    finalImage.Dispose();
                }
                _logger.LogError($"Unhandled Exception :{ex.Message}");
                throw;
            }
            finally
            {
                foreach (var image in images)
                {
                    image.Dispose();
                }
            }
        }

        private void CreateCSS(List<Style> styles, string targetDirectory)
        {
            var cssFile = Path.Combine(targetDirectory, "style.css");
            using (var streamWriter = new StreamWriter(cssFile, false))
            {
                foreach (var style in styles)
                {
                    streamWriter.WriteLine(style.ToString());
                }
            }
        }
    }
}
