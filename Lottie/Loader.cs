using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Lottie.LottieData;
using CommunityToolkit.WinUI.Lottie.LottieData.Optimization;
using CommunityToolkit.WinUI.Lottie.LottieData.Serialization;
using CommunityToolkit.WinUI.Lottie.LottieToWinComp;

namespace CommunityToolkit.WinUI.Lottie
{
    public abstract class Loader : IDisposable
    {
        // Identifies the bound property names in SourceMetadata.
        static readonly Guid s_propertyBindingNamesKey = new Guid
            (
             "A115C46A-254C-43E6-A3C7-9DE516C3C3C8"
            );

        public static async Task<LottieComposition> LoadAsync
        (
            string              fileName,
            LottieVisualOptions options = LottieVisualOptions.All
        )
        {
            Loader loader = null;

            //读取.lottie文件(其实就是.zip文件)
            if (fileName.EndsWith
                    (
                     ".lottie",
                     StringComparison.OrdinalIgnoreCase
                    ) ||
                fileName.EndsWith
                    (
                     ".zip",
                     StringComparison.OrdinalIgnoreCase
                    )
               )
            {
                loader = new ZipFileLoader
                    (
                     fileName
                    );
            }
            //读取 .json 文件
            else if (fileName.EndsWith
                         (
                          ".json",
                          StringComparison.OrdinalIgnoreCase
                         )
                    )
            {
                loader = new JsonFileLoader
                    (
                     fileName
                    );
            }

            if (loader is null)
            {
                throw new FormatException
                    (
                     "unsupported file format."
                    );
            }


            return await loader.LoadAsyncInner
                       (
                        options
                       );
        }


        protected readonly string FileName;

        public Loader
        (
            string fileName
        )
        {
            FileName = fileName;
        }


        private protected async Task<LottieComposition> LoadAsyncInner
        (
            LottieVisualOptions options
        )
        {
            Stream stream = null;

            if (File.Exists
                    (
                     FileName
                    ))
            {
                stream = await GetJsonStreamAsync();
            }


            if (stream is null)
            {
                throw new ArgumentException
                    (
                     "无法处理指定的Json数据流",
                     nameof(stream)
                    );
            }

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
                              stream,
                              LottieCompositionReader.Options.IgnoreMatchNames,
                              out var readerIssues
                             );

                     if (lottieComposition is not null)
                     {
                         //读取外置图像
                         ReadExternalImageAssets(lottieComposition);


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

        protected abstract void ReadExternalImageAssets(LottieComposition lottieComposition);

        protected abstract Task<Stream> GetJsonStreamAsync
        (
        );


        static IReadOnlyList<Issue> ToIssues
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

        public abstract void Dispose();
    }
}