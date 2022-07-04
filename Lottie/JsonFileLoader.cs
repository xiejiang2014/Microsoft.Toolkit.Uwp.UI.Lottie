// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Lottie.LottieData;

namespace CommunityToolkit.WinUI.Lottie
{
    public sealed class JsonFileLoader : Loader
    {
        public JsonFileLoader
        (
            string fileName
        ) : base
            (
             fileName
            )
        {
        }

        protected override Task<Stream> GetJsonStreamAsync
        (
        )
        {
            return Task.FromResult<Stream>
                (
                 new FileStream
                     (
                      FileName,
                      FileMode.Open,
                      FileAccess.Read
                     )
                );
        }

        protected override void ReadExternalImageAssets(LottieComposition lottieComposition)
        {

        }


        public override void Dispose()
        {
            // Nothing to dispose
        }
    }
}