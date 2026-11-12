#nullable enable
using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Simulation.Display
{
    /// <summary>
    /// When a paused courier station's couriers are all home (<c>04</c> section 19, CR-0008 D24), for the wait timer in its menu. <b>Display
    /// only, never authority.</b> The next snapshot replaces whatever it showed.
    /// </summary>
    public static class PauseDisplay
    {
        /// <summary>
        /// The city-clock second the last courier of a station is home: over its couriers walking to a stop, their arrival plus the walk from
        /// that stop home (with the city's walk bonus now); over its couriers walking home, their arrival. Null when none of them is out (all at
        /// the station or resting). Meant for a paused station; for another it says when the couriers would be home if it were paused now.
        /// </summary>
        /// <param name="city">The city.</param>
        /// <param name="content">The game content.</param>
        /// <param name="stationId">The courier station's building id.</param>
        /// <returns>The second, or null.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> or <paramref name="content"/> is null.</exception>
        public static long? LastCourierHomeAt(CityState city, GameContent content, int stationId)
        {
            if (city is null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            int station = city.IndexOfBuilding(stationId);
            if (station < 0)
            {
                return null;
            }

            WalkTimes? walks = null;
            long? last = null;
            IReadOnlyList<Courier> couriers = city.Couriers;
            for (int index = 0; index < couriers.Count; index++)
            {
                Courier courier = couriers[index];
                if (courier.StationId != stationId || !courier.ArrivesAt.HasValue)
                {
                    continue;
                }

                long home = courier.ArrivesAt.Value;
                if (courier.State == CourierState.Walking)
                {
                    int stop = StopIndex(city, courier);
                    if (stop >= 0)
                    {
                        walks ??= new WalkTimes(city, content);
                        home = checked(home + walks.Seconds(stop, station));
                    }
                }

                last = last.HasValue ? Math.Max(last.Value, home) : home;
            }

            return last;
        }

        /// <summary>The building index a walking courier walks to: its route's stop, or its first line's source or target; −1 when it has neither.</summary>
        private static int StopIndex(CityState city, Courier courier)
        {
            if (city.TryGetRoute(courier.StationId, courier.Slot, out Route? route))
            {
                return courier.ToStop >= 1 && courier.ToStop <= route.Stops.Count ? city.IndexOfBuilding(route.Stops[courier.ToStop - 1].BuildingId) : -1;
            }

            if (city.TryGetFirstLine(courier.StationId, courier.Slot, out DeliveryLine? line))
            {
                return city.IndexOfBuilding(courier.ToStop == 1 ? line.SourceId : line.TargetId);
            }

            return -1;
        }
    }
}
