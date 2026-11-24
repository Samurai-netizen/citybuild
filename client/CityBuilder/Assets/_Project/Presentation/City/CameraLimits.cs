#nullable enable

using System;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Where the city camera may look (M1-03): the smallest rectangle of tiles that holds every open tile of the map, so the player pans over
    /// open land and the mist at its edge, not over the whole map. Content without a map has every tile open: the whole grid. Display only.
    /// </summary>
    public static class CameraLimits
    {
        /// <summary>The tiles of the open land of <paramref name="city"/> as one rectangle (<c>xMax</c> and <c>yMax</c> one past the last tile).</summary>
        /// <param name="city">The displayed city, whose completed research opens areas.</param>
        /// <param name="content">The content with the map.</param>
        /// <returns>The rectangle; the whole grid when the content has no map.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static RectInt Of(CityState city, GameContent content)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            int width = content.Rules.GridWidth;
            int height = content.Rules.GridHeight;
            if (content.Map == null)
            {
                return new RectInt(0, 0, width, height);
            }

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!CityQueries.IsLandOpen(city, content, x, y))
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            return maxX < 0 ? new RectInt(0, 0, width, height) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
