using System.Collections.Generic;
using Godot;
using NXRFirearm;

namespace NXRNpc;


/// <summary>
/// Muzzle component for the "Guy Spawner" gun.
/// Every time the firearm fires it launches a fresh <see cref="RagdollGuy"/> out of the barrel.
/// No menu, no ammo types, just guys.
/// </summary>
[GlobalClass]
public partial class GuySpawner : Node3D
{

    #region Exported:
    [Export] private PackedScene _guyScene;
    /// <summary> Where spawned guys get parented. Defaults to the current scene. </summary>
    [Export] private Node3D _spawnParent;
    [Export] private float _spawnDistance = 0.6f;
    [Export] private float _launchSpeed = 6.0f;
    [Export] private float _spread = 0.08f;
    [Export] private float _upwardBoost = 1.5f;
    /// <summary> Oldest guys get cleaned up past this count so the range doesn't melt. </summary>
    [Export] private int _maxGuys = 12;
    [Export] private bool _spawnUpright = true;
    #endregion


    private Firearm _firearm;
    private readonly List<RagdollGuy> _spawned = new();


    public override void _Ready()
    {
        _firearm = FirearmUtil.GetFirearmFromParentOrOwner(this);

        if (IsInstanceValid(_firearm))
        {
            _firearm.OnFire += OnFire;
        }
    }


    private void OnFire()
    {
        SpawnGuy();
    }


    /// <summary> Spawn one guy at the muzzle and fling him forward. </summary>
    public RagdollGuy SpawnGuy()
    {
        if (_guyScene == null) return null;

        Node3D parent = IsInstanceValid(_spawnParent) ? _spawnParent : GetTree().CurrentScene as Node3D;
        if (parent == null) parent = GetParent<Node3D>();
        if (parent == null) return null;

        RagdollGuy guy = _guyScene.Instantiate() as RagdollGuy;
        if (guy == null) return null;

        Vector3 forward = -GlobalTransform.Basis.Z;
        Vector3 spawnPoint = GlobalPosition + forward * _spawnDistance;

        // build the guy already sitting at the right spot so his limbs spawn in place
        Basis basis = Basis.Identity;
        if (_spawnUpright)
        {
            Vector3 flat = new Vector3(forward.X, 0, forward.Z);
            if (flat.LengthSquared() > 0.0001f)
            {
                basis = Basis.LookingAt(flat.Normalized(), Vector3.Up);
            }
        }
        else
        {
            basis = GlobalTransform.Basis.Orthonormalized();
        }

        Transform3D globalXform = new Transform3D(basis, spawnPoint);
        guy.Transform = parent.GlobalTransform.AffineInverse() * globalXform;

        parent.AddChild(guy);

        Vector3 velocity = forward * _launchSpeed;
        velocity += Vector3.Up * _upwardBoost;
        velocity += new Vector3(
            (float)GD.RandRange(-_spread, _spread),
            (float)GD.RandRange(-_spread, _spread),
            (float)GD.RandRange(-_spread, _spread)) * _launchSpeed;

        guy.Launch(velocity);

        Track(guy);
        return guy;
    }


    private void Track(RagdollGuy guy)
    {
        _spawned.Add(guy);
        _spawned.RemoveAll(g => !IsInstanceValid(g));

        while (_spawned.Count > _maxGuys)
        {
            RagdollGuy oldest = _spawned[0];
            _spawned.RemoveAt(0);
            if (IsInstanceValid(oldest)) oldest.QueueFree();
        }
    }
}
