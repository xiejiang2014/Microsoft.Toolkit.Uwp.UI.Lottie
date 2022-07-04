#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Lottie.LottieData;
using Path = CommunityToolkit.WinUI.Lottie.LottieData.Path;

namespace CommunityToolkit.WinUI.Lottie
{
    public class ZipFileLoader : Loader
    {
        private Stream     _zipFileStream;
        private ZipArchive _zipArchive;

        public ZipFileLoader
        (
            string fileName
        ) : base
            (
             fileName
            )
        {
            _zipFileStream = new FileStream
                (
                 FileName,
                 FileMode.Open,
                 FileAccess.Read,
                 FileShare.Read
                );

            _zipArchive = new ZipArchive
                (
                 _zipFileStream,
                 ZipArchiveMode.Read
                );
        }

        protected override async Task<Stream> GetJsonStreamAsync
        (
        )
        {
            var zipArchiveEntry = _zipArchive.GetEntry
                (
                 "data.json"
                );

            if (zipArchiveEntry is null)
            {
                return null;
            }

            return zipArchiveEntry.Open();
        }

        protected override void ReadExternalImageAssets
        (
            LottieComposition lottieComposition
        )
        {
            var imageEntries = _zipArchive.Entries
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


        public override void Dispose()
        {
            // Nothing to dispose
        }
    }
}