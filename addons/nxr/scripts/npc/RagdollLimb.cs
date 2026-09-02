using Godot;
using NXRInteractable;

namespace NXRNpc;


/// <summary>
/// A single grabbable body part of a <see cref="RagdollGuy"/>. 
/// It is a normal NXR Interactable so hands can grab it, throw it, and drag the whole guy around.
/// </summary>
[GlobalClass]
public partial class RagdollLimb : Interactable
{
    /// <summary> Owning guy. Set by <see cref="RagdollGuy"/> when the body is built. </summary>
    public RagdollGuy Guy { get; set; }

    /// <summary> How much this limb contributes to "is this guy standing" checks. </summary>
    public bool IsCore { get; set; } = false;


    public override void _Ready()
    {
        base._Ready();

        AddToGroup("ragdoll_limb");

        OnGrabbed += Grabbed;
        OnFullDropped += FullDropped;
    }


    private void Grabbed(Interactable interactable, Interactor interactor)
    {
        Guy?.OnLimbGrabbed(this);
    }


    private void FullDropped()
    {
        Guy?.OnLimbDropped(this);
    }


    /// <summary> Called by NXR's FirearmRay when a bullet lands on this limb. </summary>
    public void hit(Node3D from, Vector3 at)
    {
        Vector3 dir = (GlobalPosition - at);
        if (dir.LengthSquared() < 0.0001f) dir = -GlobalBasis.Z;

        Hit(dir.Normalized() * 4.0f, at);
    }


    /// <summary> Knock the limb (and the guy) around. </summary>
    public void Hit(Vector3 impulse, Vector3 at)
    {
        if (Freeze) return;

        ApplyImpulse(impulse, at - GlobalPosition);
        Guy?.GoLimp(1.5f);
    }
}
