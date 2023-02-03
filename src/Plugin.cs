using BepInEx;
using System;
using System.Linq;
using System.Security.Permissions;

#pragma warning disable CS0618 // Do not remove the following line.
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace OshaGates;

[BepInPlugin("com.dual.osha-gates", "OSHA Compliant Gates", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    const int LeftDoor = 0;
    const int MiddleDoor = 1;
    const int RightDoor = 2;

    private bool ComingFromLeft(RegionGate gate) => gate.letThroughDir;

    private bool GatesIgnore(PhysicalObject obj)
    {
        return obj is Overseer or VoidSpawn;
    }

    private bool SafeToClose(RegionGate gate, int door)
    {
        // These are hardcoded in vanilla.
        float x1 = (14 + door * 9) * 20;
        float x2 = (16 + door * 9) * 20;
        float y1 = 8 * 20;
        float y2 = 17 * 20;

        FloatRect gateBox = new(x1, y1, x2, y2);

        foreach (var obj in gate.room.updateList.OfType<PhysicalObject>()) {
            if (GatesIgnore(obj)) {
                continue;
            }
            foreach (var chunk in obj.bodyChunks) {
                FloatRect gateBoxCorrected = gateBox;

                // Account for body chunk's radius to ensure the *whole* creature is outside the door. Don't inline this variable.
                gateBoxCorrected.Grow(chunk.rad);

                if (gateBoxCorrected.Vector2Inside(chunk.pos)) {
                    return false;
                }
            }
        }

        return true;
    }

    private bool AllCreaturesThrough(RegionGate gate)
    {
        const float LeftSideInner = 16 * 20;
        const float RightSideInner = (14 + 2 * 9) * 20;
        const float MiddleCenter = (15 + 1 * 9) * 20;

        foreach (var obj in gate.room.updateList.OfType<PhysicalObject>()) {
            if (GatesIgnore(obj)) {
                continue;
            }
            foreach (var chunk in obj.bodyChunks) {
                // If coming from left, chunk is past left door, but not past middle door, then abort.
                if (ComingFromLeft(gate) && chunk.pos.x + chunk.rad > LeftSideInner && chunk.pos.x + chunk.rad < MiddleCenter) {
                    return false;
                }
                // If coming from right, chunk is past right door, but not past middle door, then abort.
                else if (!ComingFromLeft(gate) && chunk.pos.x - chunk.rad < RightSideInner && chunk.pos.x - chunk.rad > MiddleCenter) {
                    return false;
                }
            }
        }
        return true;
    }

    public void OnEnable()
    {
        On.RegionGate.PlayersStandingStill += RegionGate_PlayersStandingStill;
        On.RegionGate.AllPlayersThroughToOtherSide += RegionGate_AllPlayersThroughToOtherSide;
    }

    private bool RegionGate_PlayersStandingStill(On.RegionGate.orig_PlayersStandingStill orig, RegionGate gate)
    {
        return orig(gate) && SafeToClose(gate, ComingFromLeft(gate) ? LeftDoor : RightDoor);
    }

    private bool RegionGate_AllPlayersThroughToOtherSide(On.RegionGate.orig_AllPlayersThroughToOtherSide orig, RegionGate gate)
    {
        return orig(gate) && SafeToClose(gate, MiddleDoor) && AllCreaturesThrough(gate);
    }
}
