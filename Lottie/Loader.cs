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

        /// <summary>
        /// Asynchonously loads an <see cref="AnimatedVisualFactory"/> that can be
        /// used to instantiate IAnimatedVisual instances.
        /// </summary>
        /// <param name="jsonLoader">A delegate that asynchronously loads the JSON for
        /// a Lottie file.</param>
        /// <param name="imageLoader">A delegate that loads images that support a Lottie file.</param>
        /// <param name="options">Options.</param>
        /// <returns>An <see cref="AnimatedVisualFactory"/> that can be used
        /// to instantiate IAnimatedVisual instances.</returns>
        private protected static async Task<LottieComposition> LoadAsync
        (
            Func<Task<(string? name, Stream? stream)>> jsonLoader,
            LottieVisualOptions                        options
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


            // Get the file name and JSON contents.
            var (fileName, jsonStream) = await jsonLoader();

            if (diagnostics is not null)
            {
                diagnostics.FileName = fileName ?? string.Empty;
                diagnostics.ReadTime = timeMeasurer.GetElapsedAndRestart();
            }

            if (jsonStream is null)
            {
                throw new ApplicationException
                    (
                     "无法处理指定的Json数据流"
                    );
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