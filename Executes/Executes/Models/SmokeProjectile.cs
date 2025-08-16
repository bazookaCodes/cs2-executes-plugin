using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using System.Runtime.InteropServices;

namespace Executes.Models
{
    public class SmokeProjectile
    {
        public static string smokeGrenadeProjectileWindowsSig =
            @"48 8B C4 48 89 58 ? 48 89 68 ? 48 89 70 ? 57 41 56 41 57 48 81 EC ? ? ? ? 48 8B B4 24 ? ? ? ? 4D 8B F8";

        public static string smokeGrenadeProjectileLinuxSig =
            @"55 4C 89 C1 48 89 E5 41 57 49 89 FF 41 56 45 89 CE";

        public static string smokeGrenadeProjectileSig = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? smokeGrenadeProjectileLinuxSig : smokeGrenadeProjectileWindowsSig;
        public static MemoryFunctionWithReturn<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int> CSmokeGrenadeProjectile_CreateFunc = new(smokeGrenadeProjectileSig);

        public static nint Create(Vector position, QAngle angle, Vector velocity, CsTeam team)
        {
            return CSmokeGrenadeProjectile_CreateFunc.Invoke(
                position.Handle,
                angle.Handle,
                velocity.Handle,
                velocity.Handle,
                nint.Zero,
                45,
                (byte)team
            );
        }
    }
}
