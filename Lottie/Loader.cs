#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Lottie.LottieData;
using CommunityToolkit.WinUI.Lottie.LottieData.Optimization;
using CommunityToolkit.WinUI.Lottie.LottieData.Serialization;
using CommunityToolkit.WinUI.Lottie.LottieToWinComp;
using Path = System.IO.Path;

namespace CommunityToolkit.WinUI.Lottie
{
    public static class Loader
    {
        // Identifies the bound property names in SourceMetadata.
        static readonly Guid s_propertyBindingNamesKey = new Guid
            (
             "A115C46A-254C-43E6-A3C7-9DE516C3C3C8"
            );


        public static Task<LottieComposition> LoadAsync
        (
            Stream              stream,
            LottieVisualOptions options = LottieVisualOptions.All
        )
        {
            return LoadAsync
                (
                 stream,
                 null,
                 options
                );
        }

        public static async Task<LottieComposition> LoadAsync
        (
            string              fileName,
            LottieVisualOptions options = LottieVisualOptions.All
        )
        {
            Stream?     jsonStream = null;
            ZipArchive? zipArchive = null;

            //读取.lottie文件(其实就是.zip文件)

            var fileExt = Path
                         .GetExtension
                              (
                               fileName
                              )
                         .ToLower();

            switch (fileExt)
            {
                case ".lottie":
                case ".zip":
                    (jsonStream, zipArchive) = GetZipArchiveStream
                        (
                         fileName
                        );
                    break;

                case ".json":
                    jsonStream = GetJsonFileStream
                        (
                         fileName
                        );

                    break;
            }


            if (jsonStream is null)
            {
                throw new FormatException
                    (
                     "unsupported file format."
                    );
            }


            return await LoadAsync
                       (
                        jsonStream,
                        zipArchive,
                        options
                       );
        }


        private static (Stream? jsonStream, ZipArchive zipArchive) GetZipArchiveStream
        (
            string fileName
        )
        {
            var zipFileStream = new FileStream
                (
                 fileName,
                 FileMode.Open,
                 FileAccess.Read,
                 FileShare.Read
                );

            var zipArchive = new ZipArchive
                (
                 zipFileStream,
                 ZipArchiveMode.Read
                );

            var zipArchiveEntry = zipArchive.GetEntry
                (
                 "data.json"
                );

            return (zipArchiveEntry?.Open(), zipArchive);
        }

        private static Stream? GetJsonFileStream
        (
            string fileName
        )
        {
            return new FileStream
                (
                 fileName,
                 FileMode.Open,
                 FileAccess.Read
                );
        }


        private static async Task<LottieComposition> LoadAsync
        (
            Stream              jsonStream,
            ZipArchive?         zipArchive,
            LottieVisualOptions options
        )
        {
            LottieVisualDiagnostics? diagnostics  = null;
            var                      timeMeasurer = TimeMeasurer.Create();

            if (options.HasFlag
                    (
                     LottieVisualOptions.IncludeDiagnostics
                    )
               )
            {
                diagnostics = new LottieVisualDiagnostics { Options = options };
            }


            if (diagnostics is not null)
            {
                diagnostics.ReadTime = timeMeasurer.GetElapsedAndRestart();
            }


            // Parsing large Lottie files can take significant time. Do it on
            // another thread.
            LottieComposition? lottieComposition = null;
            await Task.Run
                (
                 () =>
                 {
                     lottieComposition =
                         LottieCompositionReader.ReadLottieCompositionFromJsonStream
                             (
                              jsonStream,
                              LottieCompositionReader.Options.IgnoreMatchNames,
                              out var readerIssues
                             );

                     if (lottieComposition is not null)
                     {
                         //读取外置图像
                         ReadExternalImageAssets
                             (
                              lottieComposition,
                              zipArchive
                             );


                         lottieComposition = LottieMergeOptimizer.Optimize
                             (
                              lottieComposition
                             );
                     }

                     if (diagnostics is not null)
                     {
                         diagnostics.JsonParsingIssues = ToIssues
                             (
                              readerIssues
                             );
                     }
                 }
                );

            if (diagnostics is not null)
            {
                diagnostics.ParseTime = timeMeasurer.GetElapsedAndRestart();
            }


            //到此已经得到了完整的 lottieComposition 对象
            if (lottieComposition is null)
            {
                throw new ApplicationException
                    (
                     "Lottie 数据读取失败."
                    );
            }

            if (diagnostics is not null)
            {
                // Save the LottieComposition in the diagnostics so that the xml and codegen
                // code can be derived from it.
                diagnostics.LottieComposition = lottieComposition;

                // Validate the composition and report if issues are found.
                diagnostics.LottieValidationIssues = ToIssues
                    (
                     LottieCompositionValidator.Validate
                         (
                          lottieComposition
                         )
                    );
                diagnostics.ValidationTime = timeMeasurer.GetElapsedAndRestart();
            }


            return lottieComposition;
        }

        private static void ReadExternalImageAssets
        (
            LottieComposition lottieComposition,
            ZipArchive?       zipArchive
        )
        {
            if (zipArchive is null)
            {
                return;
            }


            var imageEntries = zipArchive.Entries
                                         .Where
                                              (
                                               v =>
                                                   v.Name.EndsWith
                                                       (
                                                        "png",
                                                        StringComparison.OrdinalIgnoreCase
                                                       ) ||
                                                   v.Name.EndsWith
                                                       (
                                                        "jpg",
                                                        StringComparison.OrdinalIgnoreCase
                                                       ) ||
                                                   v.Name.EndsWith
                                                       (
                                                        "jpeg",
                                                        StringComparison.OrdinalIgnoreCase
                                                       )
                                              );

            foreach (var zipArchiveEntry in imageEntries)
            {
                var externalImageAsset = lottieComposition.Assets
                                                          .OfType<ExternalImageAsset>()
                                                          .FirstOrDefault
                                                               (
                                                                v => v.FileName == zipArchiveEntry.Name
                                                               );

                //将 externalImageAsset 转换为 EmbeddedImageAsset
                if (externalImageAsset is not null)
                {
                    lottieComposition.Assets.Remove
                        (
                         externalImageAsset
                        );

                    byte[] buffer = new byte[zipArchiveEntry.Length];
                    zipArchiveEntry.Open()
                                   .Read
                                        (
                                         buffer,
                                         0,
                                         (int)zipArchiveEntry.Length
                                        );


                    var format = System.IO.Path.GetExtension
                                     (
                                      zipArchiveEntry.Name
                                     ) switch

                                 {
                                     ".png"  => "png",
                                     ".jpg"  => "jpg",
                                     ".jpeg" => "jpg",
                                     _ => throw new FormatException
                                              (
                                               "unsupported file format."
                                              )
                                 };

                    var embeddedImageAsset = new EmbeddedImageAsset
                        (
                         externalImageAsset.Id,
                         externalImageAsset.Width,
                         externalImageAsset.Height,
                         buffer,
                         format
                        );

                    lottieComposition.Assets.Add
                        (
                         embeddedImageAsset
                        );
                }
            }
        }


        private static IReadOnlyList<Issue> ToIssues
        (
            IEnumerable<(string Code, string Description)> issues
        ) =>
            issues.Select
                   (
                    issue => new Issue
                        (
                         code: issue.Code,
                         description: issue.Description
                        )
                   )
                  .ToArray();

        static IReadOnlyList<Issue> ToIssues
        (
            IEnumerable<TranslationIssue> issues
        ) =>
            issues.Select
                   (
                    issue => new Issue
                        (
                         code: issue.Code,
                         description: issue.Description
                        )
                   )
                  .ToArray();


        // Specializes the Stopwatch to do just the one thing we need of it - get the time
        // elapsed since the last call then restart the Stopwatch to start measuring again.
        readonly struct TimeMeasurer
        {
            readonly Stopwatch _stopwatch;

            TimeMeasurer
            (
                Stopwatch stopwatch
            ) =>
                _stopwatch = stopwatch;

            public static TimeMeasurer Create() =>
                new TimeMeasurer
                    (
                     Stopwatch.StartNew()
                    );

            public TimeSpan GetElapsedAndRestart()
            {
                var result = _stopwatch.Elapsed;
                _stopwatch.Restart();
                return result;
            }
        }
    }
}