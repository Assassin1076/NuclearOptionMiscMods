using BepInEx;
using HarmonyLib;
using UnityEngine;
using static BulletSim;

[BepInPlugin("fix.kinematics.trajectorysim", "TrajectorySim Fix", "1.0.1")]
public class TrajectorySimFixPlugin : BaseUnityPlugin
{
    void Awake()
    {
        new Harmony("fix.kinematics.trajectorysim").PatchAll();
    }
}

[HarmonyPatch(typeof(Kinematics), nameof(Kinematics.TrajectorySim))]
public static class Patch_TrajectorySim
{
    static bool Prefix(
        bool debug,
        WeaponInfo weaponInfo,
        Vector3 initialVelocity,
        GlobalPosition initialPosition,
        GlobalPosition targetPos,
        Vector3 targetVel,
        Vector3 targetAccel,
        float timeStep,
        out Vector3 __result,
        out float timeToTarget)
    {
        //不使用传递的部分参数，使用常数来完成功能
        const int maxSteps = 2000;
        float dt = Time.fixedDeltaTime;

        GlobalPosition bulletPos = initialPosition;
        Vector3 bulletVel = initialVelocity;

        GlobalPosition simTargetPos = targetPos;
        Vector3 simTargetVel = targetVel;

        float t = 0f;

        float bestDist = float.MaxValue;
        Vector3 bestBulletPos = bulletPos.AsVector3();
        Vector3 bestTargetPos = simTargetPos.AsVector3();
        float bestTime = 0f;


        for (int i = 0; i < maxSteps; i++)
        {

            simTargetVel += targetAccel * dt;
            simTargetPos += simTargetVel * dt;

            // 与bullet physics一致
            bulletVel.y -= 9.81f * dt * weaponInfo.gravMult;

            bulletVel -= bulletVel.sqrMagnitude
                         * weaponInfo.dragCoef
                         * dt
                         * bulletVel.normalized
                         / weaponInfo.muzzleVelocity;

            bulletPos += bulletVel * dt;

            Vector3 diff = bulletPos - simTargetPos;
            float dist = diff.sqrMagnitude;

            if (dist < bestDist)
            {
                bestDist = dist;
                bestBulletPos = bulletPos.AsVector3();
                bestTargetPos = simTargetPos.AsVector3();
                bestTime = t;
            }

            t += dt;

            // 如果已经开始远离且误差扩大，可以提前停止
            if (i > 10 && dist > bestDist * 2f)
                break;
        }


        Vector3 finalDiff = bestBulletPos - bestTargetPos;

        __result = Vector3.ProjectOnPlane(finalDiff, bulletVel);
        timeToTarget = bestTime;

        return false;
    }
}