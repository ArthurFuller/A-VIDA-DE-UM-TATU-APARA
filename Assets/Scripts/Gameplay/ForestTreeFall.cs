using System;
using DG.Tweening;
using UnityEngine;

public sealed class ForestTreeFall : MonoBehaviour
{
    [Serializable]
    private sealed class TreeParts
    {
        public Transform trunk;
        public Transform canopy;
        public Transform impactTip;
        [NonSerialized] public Vector3 trunkPosition;
        [NonSerialized] public Vector3 canopyPosition;
        [NonSerialized] public Quaternion canopyRotation;
        [NonSerialized] public Pose legacyTrunk;
        [NonSerialized] public Pose legacyCanopy;
        [NonSerialized] public bool fallen;
        [NonSerialized] public Tween tween;
    }

    [SerializeField] private TreeParts[] trees;
    [SerializeField, Range(30f, 100f)] private float fallAngle = 86f;
    [SerializeField, Min(0.1f)] private float fallDuration = 0.72f;
    [SerializeField, Min(0f)] private float impactBounceAngle = 7f;
    [SerializeField, Min(0f)] private float impactBounceDuration = 0.22f;
    [SerializeField, Min(0f)] private float impactHold = 0.12f;

    public Vector3 LastImpactPosition { get; private set; }

    private void Awake()
    {
        if (trees == null) return;
        foreach (TreeParts tree in trees)
        {
            if (tree?.trunk == null || tree.canopy == null) continue;
            tree.trunkPosition = tree.trunk.position;
            tree.canopyPosition = tree.canopy.localPosition;
            tree.canopyRotation = tree.canopy.localRotation;
            if (tree.impactTip == null)
            {
                tree.legacyTrunk = new Pose(tree.trunk.position, tree.trunk.rotation);
                tree.legacyCanopy = new Pose(tree.canopy.position, tree.canopy.rotation);
            }
        }
    }

    public bool TryGetFacingPosition(Vector3 workerPosition, out Vector3 facingPosition)
    {
        TreeParts tree = FindNearestTree(workerPosition, true) ?? FindNearestTree(workerPosition, false);
        facingPosition = tree != null ? tree.trunkPosition : workerPosition;
        return tree != null;
    }

    private TreeParts FindNearestTree(Vector3 workerPosition, bool standingOnly)
    {
        if (trees == null) return null;
        TreeParts selected = null;
        float closest = float.PositiveInfinity;
        foreach (TreeParts tree in trees)
        {
            if (tree?.trunk == null || tree.canopy == null || (standingOnly && tree.fallen)) continue;
            Vector3 delta = tree.trunkPosition - workerPosition;
            if (tree.impactTip != null) delta.y = 0f;
            if (delta.sqrMagnitude >= closest) continue;
            closest = delta.sqrMagnitude;
            selected = tree;
        }
        return selected;
    }

    public void FallNear(Vector3 workerPosition, Action onImpact)
    {
        LastImpactPosition = workerPosition + Vector3.up;
        TreeParts selected = FindNearestTree(workerPosition, true);
        if (selected == null)
        {
            onImpact?.Invoke();
            return;
        }

        selected.fallen = true;
        AudioManager.Play(SoundCue.TreeCrack);
        Vector3 direction = selected.trunkPosition - workerPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.04f) direction = Vector3.right;
        Vector3 axis = Vector3.Cross(Vector3.up, direction).normalized;
        Transform hinge = selected.canopy;
        bool legacy = selected.impactTip == null;
        Vector3 localAxis = hinge.parent != null ? hinge.parent.InverseTransformDirection(axis) : axis;
        Vector3 legacyBase = legacy ? selected.trunkPosition - selected.trunk.up * selected.trunk.lossyScale.y : Vector3.zero;
        float drop = Mathf.Max(0f, hinge.position.y - selected.trunkPosition.y - 0.07f);
        Vector3 settle = direction.normalized * 0.24f + Vector3.down * drop;
        if (hinge.parent != null) settle = hinge.parent.InverseTransformVector(settle);
        float angle = 0f;
        void ApplyAngle(float value)
        {
            angle = value;
            if (legacy)
            {
                Quaternion rotation = Quaternion.AngleAxis(value, axis);
                RotateAround(selected.trunk, selected.legacyTrunk, legacyBase, rotation);
                RotateAround(hinge, selected.legacyCanopy, legacyBase, rotation);
                return;
            }
            hinge.localRotation = Quaternion.AngleAxis(value, localAxis) * selected.canopyRotation;
            float landing = Mathf.InverseLerp(fallAngle * 0.55f, fallAngle, value);
            hinge.localPosition = selected.canopyPosition + settle * Mathf.SmoothStep(0f, 1f, landing);
        }

        selected.tween?.Kill();
        Sequence fall = DOTween.Sequence().SetTarget(hinge);
        fall.Append(DOTween.To(() => angle, ApplyAngle, fallAngle, fallDuration).SetEase(Ease.InQuad));
        fall.AppendCallback(() => AudioManager.Play(SoundCue.TreeFall));
        if (impactBounceDuration > 0f && impactBounceAngle > 0f)
        {
            fall.Append(DOTween.To(() => angle, ApplyAngle, fallAngle - impactBounceAngle,
                impactBounceDuration * 0.5f).SetEase(Ease.OutQuad));
            fall.Append(DOTween.To(() => angle, ApplyAngle, fallAngle,
                impactBounceDuration * 0.5f).SetEase(Ease.InOutSine));
        }
        fall.AppendInterval(impactHold);
        fall.OnComplete(() =>
        {
            LastImpactPosition = selected.impactTip != null ? selected.impactTip.position : hinge.position;
            onImpact?.Invoke();
        });
        selected.tween = fall;
    }

    private static void RotateAround(Transform part, Pose initial, Vector3 pivot, Quaternion rotation)
    {
        part.SetPositionAndRotation(pivot + rotation * (initial.position - pivot), rotation * initial.rotation);
    }

    public void ResetTrees()
    {
        if (trees == null) return;
        foreach (TreeParts tree in trees)
        {
            if (tree?.trunk == null || tree.canopy == null) continue;
            tree.tween?.Kill();
            if (tree.impactTip == null)
            {
                tree.trunk.SetPositionAndRotation(tree.legacyTrunk.position, tree.legacyTrunk.rotation);
                tree.canopy.SetPositionAndRotation(tree.legacyCanopy.position, tree.legacyCanopy.rotation);
            }
            else
            {
                tree.canopy.localPosition = tree.canopyPosition;
                tree.canopy.localRotation = tree.canopyRotation;
            }
            tree.fallen = false;
        }
    }

    private void OnDestroy()
    {
        if (trees == null) return;
        foreach (TreeParts tree in trees) tree?.tween?.Kill();
    }
}
