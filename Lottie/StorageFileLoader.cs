// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Lottie;
using CommunityToolkit.WinUI.Lottie.LottieData;

namespace CommunityToolkit.WinUI.Lottie
{
    /// <summary>
    /// Loads files from a <see cref="StorageFile"/>. Supports raw
    /// JSON files and .lottie files.
    /// </summary>
    public sealed class StorageFileLoader : Loader
    {
        #region 静态

        public static async Task<LottieComposition> LoadAsync
        (
            string              fileName,
            LottieVisualOptions options = LottieVisualOptions.All
        )
        {
            if (string.IsNullOrWhiteSpace
                    (
                     fileName
                    ))
            {
                throw new ArgumentNullException
                    (
                     nameof(fileName)
                    );
            }


            //读取 .json 文件
            var loader = new StorageFileLoader
                (
                 fileName
                );
            return await LoadAsync
                       (
                        loader.GetJsonStreamAsync,
                        options
                       );
        }


        public static async Task<LottieComposition> LoadAsync
        (
            Stream              lottieStream,
            LottieVisualOptions options = LottieVisualOptions.All
        )
        {
            var loadingTask = Task.FromResult<(string?, Stream?)>
                (
                 ("",
                  lottieStream)
                );


            return await LoadAsync
                       (
                        () => loadingTask,
                        options
                       );
        }

        #endregion


        #region 构造

        StorageFileLoader
        (
            string fileName
        )
        {
            _fileName = fileName;
        }

        #endregion


        readonly string _fileName;

        Task<(string?, Stream?)> GetJsonStreamAsync()
        {
            var randomAccessStream = new FileStream
                (
                 _fileName,
                 FileMode.Open,
                 FileAccess.Read
                );


            return Task.FromResult<(string?, Stream?)>
                (
                 (System.IO.Path.GetFileName
                      (
                       _fileName
                      ), randomAccessStream)
                );
        }

        public override void Dispose()
        {
            // Nothing to dispose
        }
    }
}