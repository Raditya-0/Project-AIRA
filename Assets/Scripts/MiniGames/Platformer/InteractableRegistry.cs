using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AIRA.MiniGames.Platformer
{
    public static class InteractableRegistry
    {
        private static readonly List<PressurePlate> _plates    = new();
        private static readonly List<PlateReactive> _reactives = new();

        // Daftarkan pressure plate
        public static void RegisterPlate(PressurePlate plate)
        {
            if (!_plates.Contains(plate)) _plates.Add(plate);
        }

        // Hapus pressure plate
        public static void UnregisterPlate(PressurePlate plate)
        {
            _plates.Remove(plate);
        }

        // Daftarkan reactive object
        public static void RegisterReactive(PlateReactive reactive)
        {
            if (!_reactives.Contains(reactive)) _reactives.Add(reactive);
        }

        // Hapus reactive object
        public static void UnregisterReactive(PlateReactive reactive)
        {
            _reactives.Remove(reactive);
        }

        // Ambil semua plate dalam radius
        public static List<PressurePlate> GetPlatesInRadius(Vector2 origin, float radius)
        {
            return _plates
                .Where(p => p != null && Vector2.Distance(origin, p.transform.position) <= radius)
                .ToList();
        }

        // Ambil semua reactive dalam radius
        public static List<PlateReactive> GetReactivesInRadius(Vector2 origin, float radius)
        {
            return _reactives
                .Where(r => r != null && Vector2.Distance(origin, r.transform.position) <= radius)
                .ToList();
        }

        // Reset saat scene unload
        public static void Clear()
        {
            _plates.Clear();
            _reactives.Clear();
        }
    }
}
