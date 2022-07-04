// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CommunityToolkit.WinUI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
        sealed class AssetCollection : IEnumerable<Asset>
    {
        readonly List<Asset>               _assets;
        readonly Dictionary<string, Asset> _assetsById = new Dictionary<string, Asset>();

        public AssetCollection
        (
            IEnumerable<Asset> assets
        )
        {
            _assets = assets.ToList();

            foreach (var asset in _assets)
            {
                // Ignore assets that have the same ID as an asset already added.
                // Assets should have unique IDs, however it is easy to be resilient to
                // this case.
                if (!_assetsById.ContainsKey
                        (
                         asset.Id
                        ))
                {
                    _assetsById.Add
                        (
                         asset.Id,
                         asset
                        );
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="Asset"/> with the given id, or null if not found.
        /// </summary>
        /// <returns>The <see cref="Asset"/> with the given id, or null if not found.</returns>
        public Asset? GetAssetById
        (
            string id
        )
        {
            if (id is null)
            {
                return null;
            }

            return _assetsById.TryGetValue
                       (
                        id,
                        out var result
                       ) ?
                       result :
                       null;
        }


        public void Add
        (
            Asset asset
        )
        {
            _assets.Add
                (
                 asset
                );
            _assetsById.Add
                (
                 asset.Id,
                 asset
                );
        }

        public void Remove
        (
            Asset asset
        )
        {
            _assets.Remove
                (
                 asset
                );
            _assetsById.Remove
                (
                 asset.Id
                );
        }


        /// <inheritdoc/>
        public IEnumerator<Asset> GetEnumerator() => ((IEnumerable<Asset>)_assets).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<Asset>)_assets).GetEnumerator();
    }
}