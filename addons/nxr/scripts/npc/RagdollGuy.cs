using System.Collections.Generic;
using Godot;
using NXRInteractable;

namespace NXRNpc;


/// <summary>
/// A wobbly "Human Fall Flat / Gang Beasts" style ragdoll guy.
///
/// The whole body is built from code at runtime: rigid body limbs held together with joints,
/// kept upright by a spring + torque balance controller instead of animations.
/// Punch him, shoot him, or grab him by the arm and he flops, stumbles and drunkenly gets back up.
/// Every limb is an NXR Interactable, so hands can grab any part of him.
/// </summary>
[GlobalClass]
public partial class RagdollGuy : Node3D
{

    #region Exported:
    [ExportGroup("Body")]
    [Export] private float _bodyScale = 1.0f;
    [Export] private float _totalMass = 34.0f;
    [Export] private Color _skinColor = new Color(0.94f, 0.75f, 0.6f);
    [Export] private bool _randomizeColor = true;
    [Export] private Color _shirtColor = new Color(0.2f, 0.45f, 0.85f);
    [Export] private Color _pantsColor = new Color(0.18f, 0.2f, 0.26f);

    [ExportGroup("Balance")]
    [Export] private bool _canStand = true;
    /// <summary> Upward spring holding the hips over the feet. </summary>
    [Export] private float _standSpring = 60.0f;
    [Export] private float _standDamp = 8.0f;
    /// <summary> How hard the torso fights to stay vertical. </summary>
    [Export] private float _uprightStrength = 8.0f;
    [Export] private float _uprightDamp = 2.5f;
    /// <summary> Seconds of lying around before he tries to stand back up. </summary>
    [Export] private float _getUpDelay = 2.0f;

    [ExportGroup("Behavior")]
    [Export] private bool _wander = true;
    [Export] private float _walkForce = 55.0f;
    [Export] private float _wanderTimeMin = 1.5f;
    [Export] private float _wanderTimeMax = 4.0f;
    /// <summary> Random flailing added to the arms. Pure comedy value. </summary>
    [Export] private float _flail = 0.35f;
    #endregion


    #region Public:
    public RagdollLimb Hips { get; private set; }
    public RagdollLimb Chest { get; private set; }
    public RagdollLimb Head { get; private set; }
    public List<RagdollLimb> Limbs { get; private set; } = new();
    public bool Limp => _limpTimer > 0.0f;
    #endregion


    #region Private:
    private readonly List<Rid> _limbRids = new();
    private float _limpTimer = 0.0f;
    private float _downTimer = 0.0f;
    private float _wanderTimer = 0.0f;
    private float _time = 0.0f;
    private float _standHeight = 0.86f;
    private Vector3 _moveDir = Vector3.Zero;
    private int _grabCount = 0;
    #endregion


    [Signal] public delegate void FellOverEventHandler();
    [Signal] public delegate void StoodUpEventHandler();


    public override void _Ready()
    {
        AddToGroup("guys");

        if (_randomizeColor)
        {
            _shirtColor = Color.FromHsv(GD.Randf(), (float)GD.RandRange(0.4f, 0.9f), (float)GD.RandRange(0.5f, 1.0f));
            _pantsColor = Color.FromHsv(GD.Randf(), (float)GD.RandRange(0.1f, 0.6f), (float)GD.RandRange(0.15f, 0.5f));
        }

        Build();
        NewWanderDirection();
    }


    #region Body building:
    private void Build()
    {
        float s = _bodyScale;
        _standHeight = 0.82f * s;

        Material skin = MakeMaterial(_skinColor);
        Material shirt = MakeMaterial(_shirtColor);
        Material pants = MakeMaterial(_pantsColor);

        // torso
        Hips = MakeBox("Hips", new Vector3(0, 0.80f, 0) * s, new Vector3(0.30f, 0.22f, 0.20f) * s, 0.24f, pants);
        Chest = MakeBox("Chest", new Vector3(0, 1.14f, 0) * s, new Vector3(0.34f, 0.36f, 0.22f) * s, 0.28f, shirt);
        Head = MakeSphere("Head", new Vector3(0, 1.46f, 0) * s, 0.13f * s, 0.09f, skin);
        Hips.IsCore = true;
        Chest.IsCore = true;

        MakeFace(Head, s);

        // arms
        RagdollLimb armLU = MakeCapsule("UpperArmL", new Vector3(-0.23f, 1.14f, 0) * s, 0.055f * s, 0.24f * s, 0.045f, shirt);
        RagdollLimb armLL = MakeCapsule("LowerArmL", new Vector3(-0.23f, 0.86f, 0) * s, 0.05f * s, 0.24f * s, 0.035f, skin);
        RagdollLimb armRU = MakeCapsule("UpperArmR", new Vector3(0.23f, 1.14f, 0) * s, 0.055f * s, 0.24f * s, 0.045f, shirt);
        RagdollLimb armRL = MakeCapsule("LowerArmR", new Vector3(0.23f, 0.86f, 0) * s, 0.05f * s, 0.24f * s, 0.035f, skin);

        // legs
        RagdollLimb legLU = MakeCapsule("UpperLegL", new Vector3(-0.10f, 0.58f, 0) * s, 0.07f * s, 0.30f * s, 0.07f, pants);
        RagdollLimb legLL = MakeCapsule("LowerLegL", new Vector3(-0.10f, 0.25f, 0) * s, 0.06f * s, 0.28f * s, 0.06f, pants);
        RagdollLimb legRU = MakeCapsule("UpperLegR", new Vector3(0.10f, 0.58f, 0) * s, 0.07f * s, 0.30f * s, 0.07f, pants);
        RagdollLimb legRL = MakeCapsule("LowerLegR", new Vector3(0.10f, 0.25f, 0) * s, 0.06f * s, 0.28f * s, 0.06f, pants);

        // feet keep him from tipping instantly
        RagdollLimb footL = MakeBox("FootL", new Vector3(-0.10f, 0.05f, -0.04f) * s, new Vector3(0.12f, 0.08f, 0.26f) * s, 0.03f, skin);
        RagdollLimb footR = MakeBox("FootR", new Vector3(0.10f, 0.05f, -0.04f) * s, new Vector3(0.12f, 0.08f, 0.26f) * s, 0.03f, skin);

        // joints
        Cone("Waist", Hips, Chest, new Vector3(0, 0.98f, 0) * s, 35, 40);
        Cone("Neck", Chest, Head, new Vector3(0, 1.34f, 0) * s, 45, 50);

        Cone("ShoulderL", Chest, armLU, new Vector3(-0.21f, 1.26f, 0) * s, 90, 90);
        Cone("ShoulderR", Chest, armRU, new Vector3(0.21f, 1.26f, 0) * s, 90, 90);
        Hinge("ElbowL", armLU, armLL, new Vector3(-0.23f, 0.99f, 0) * s, -140, 2);
        Hinge("ElbowR", armRU, armRL, new Vector3(0.23f, 0.99f, 0) * s, -140, 2);

        Cone("HipL", Hips, legLU, new Vector3(-0.10f, 0.74f, 0) * s, 70, 30);
        Cone("HipR", Hips, legRU, new Vector3(0.10f, 0.74f, 0) * s, 70, 30);
        Hinge("KneeL", legLU, legLL, new Vector3(-0.10f, 0.41f, 0) * s, -2, 130);
        Hinge("KneeR", legRU, legRL, new Vector3(0.10f, 0.41f, 0) * s, -2, 130);
        Cone("AnkleL", legLL, footL, new Vector3(-0.10f, 0.11f, 0) * s, 35, 20);
        Cone("AnkleR", legRL, footR, new Vector3(0.10f, 0.11f, 0) * s, 35, 20);

        // normalize mass so tuning stays the same at any scale
        float total = 0.0f;
        foreach (RagdollLimb limb in Limbs) total += limb.Mass;
        foreach (RagdollLimb limb in Limbs) limb.Mass = limb.Mass / total * _totalMass;
    }


    private StandardMaterial3D MakeMaterial(Color color)
    {
        StandardMaterial3D mat = new()
        {
            AlbedoColor = color,
            Roughness = 0.8f,
            Metallic = 0.0f
        };
        return mat;
    }


    private RagdollLimb MakeLimb(string name, Vector3 localPos, Shape3D shape, Mesh mesh, float mass, Material material)
    {
        RagdollLimb limb = new()
        {
            Name = name,
            Mass = Mathf.Max(mass, 0.01f),
            Guy = this,
            ContinuousCd = true,
            CollisionLayer = 8,     // NXR interactable layer, hands look for this
            CollisionMask = 1 | 8,  // world + other props / guys
            FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic,
            LinearDamp = 0.15f,
            AngularDamp = 0.6f,
            Priority = 0.5f,
            GrabBreakDistance = 0.6f
        };

        CollisionShape3D col = new() { Shape = shape, Name = "CollisionShape3D" };
        MeshInstance3D mi = new() { Mesh = mesh, Name = "Mesh", MaterialOverride = material };

        limb.AddChild(col);
        limb.AddChild(mi);

        // makes him grabbable by NXR hands
        InteractableGrab grab = new() { Name = "InteractableGrab" };
        limb.AddChild(grab);

        AddChild(limb);
        limb.Position = localPos;

        Limbs.Add(limb);
        _limbRids.Add(limb.GetRid());
        return limb;
    }


    private RagdollLimb MakeBox(string name, Vector3 pos, Vector3 size, float mass, Material mat)
    {
        BoxShape3D shape = new() { Size = size };
        BoxMesh mesh = new() { Size = size };
        return MakeLimb(name, pos, shape, mesh, mass, mat);
    }


    private RagdollLimb MakeSphere(string name, Vector3 pos, float radius, float mass, Material mat)
    {
        SphereShape3D shape = new() { Radius = radius };
        SphereMesh mesh = new() { Radius = radius, Height = radius * 2.0f, RadialSegments = 16, Rings = 10 };
        return MakeLimb(name, pos, shape, mesh, mass, mat);
    }


    private RagdollLimb MakeCapsule(string name, Vector3 pos, float radius, float height, float mass, Material mat)
    {
        float h = Mathf.Max(height, radius * 2.05f);
        CapsuleShape3D shape = new() { Radius = radius, Height = h };
        CapsuleMesh mesh = new() { Radius = radius, Height = h, RadialSegments = 12, Rings = 4 };
        return MakeLimb(name, pos, shape, mesh, mass, mat);
    }


    private void MakeFace(RagdollLimb head, float s)
    {
        StandardMaterial3D black = MakeMaterial(new Color(0.05f, 0.05f, 0.07f));
        SphereMesh eyeMesh = new() { Radius = 0.022f * s, Height = 0.044f * s, RadialSegments = 8, Rings = 5 };

        for (int i = 0; i < 2; i++)
        {
            MeshInstance3D eye = new()
            {
                Name = i == 0 ? "EyeL" : "EyeR",
                Mesh = eyeMesh,
                MaterialOverride = black,
                Position = new Vector3(i == 0 ? -0.05f : 0.05f, 0.02f, -0.11f) * s
            };
            head.AddChild(eye);
        }
    }


    private void Cone(string name, PhysicsBody3D a, PhysicsBody3D b, Vector3 localPos, float swingDeg, float twistDeg)
    {
        ConeTwistJoint3D joint = new() { Name = name };
        AddChild(joint);
        joint.Position = localPos;
        joint.NodeA = joint.GetPathTo(a);
        joint.NodeB = joint.GetPathTo(b);
        joint.SetParam(ConeTwistJoint3D.Param.SwingSpan, Mathf.DegToRad(swingDeg));
        joint.SetParam(ConeTwistJoint3D.Param.TwistSpan, Mathf.DegToRad(twistDeg));
        joint.SetParam(ConeTwistJoint3D.Param.Softness, 0.6f);
        joint.SetParam(ConeTwistJoint3D.Param.Relaxation, 1.0f);
    }


    private void Hinge(string name, PhysicsBody3D a, PhysicsBody3D b, Vector3 localPos, float lowerDeg, float upperDeg)
    {
        HingeJoint3D joint = new() { Name = name };
        AddChild(joint);
        // hinge spins around its local Z, so aim Z along the body's X axis (elbows / knees bend front to back)
        joint.Transform = new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2.0f), localPos);
        joint.NodeA = joint.GetPathTo(a);
        joint.NodeB = joint.GetPathTo(b);
        joint.SetFlag(HingeJoint3D.Flag.UseLimit, true);
        joint.SetParam(HingeJoint3D.Param.LimitLower, Mathf.DegToRad(lowerDeg));
        joint.SetParam(HingeJoint3D.Param.LimitUpper, Mathf.DegToRad(upperDeg));
        joint.SetParam(HingeJoint3D.Param.LimitSoftness, 0.7f);
        joint.SetParam(HingeJoint3D.Param.LimitRelaxation, 1.0f);
    }
    #endregion


    #region Runtime:
    public override void _PhysicsProcess(double delta)
    {
        if (Hips == null || Chest == null) return;

        float d = (float)delta;
        _time += d;

        if (_limpTimer > 0.0f) _limpTimer -= d;

        bool upright = Chest.GlobalBasis.Y.Dot(Vector3.Up) > 0.4f;
        _downTimer = upright ? 0.0f : _downTimer + d;

        bool standing = _canStand && !Limp && _grabCount <= 0 && (upright || _downTimer > _getUpDelay);

        if (standing)
        {
            Balance(d);
            if (_wander) Wander(d);
        }

        Flail(d);
    }


    private void Balance(float delta)
    {
        float ground = GroundDistance();

        // hips ride a spring above the floor, that's what keeps him "standing"
        if (ground >= 0.0f && ground < _standHeight * 1.6f && !Hips.Freeze)
        {
            float error = _standHeight - ground;
            float support = _totalMass * 9.8f * 0.85f; // carry most of his weight for him
            float force = support + ((error * _standSpring) - (Hips.LinearVelocity.Y * _standDamp)) * _totalMass;
            force = Mathf.Clamp(force, 0.0f, support * 2.5f);
            Hips.ApplyCentralForce(Vector3.Up * force);
        }

        Upright(Chest, _uprightStrength, _uprightDamp);
        Upright(Hips, _uprightStrength * 0.6f, _uprightDamp * 0.6f);
        Upright(Head, _uprightStrength * 0.25f, _uprightDamp * 0.3f);
    }


    private void Upright(RagdollLimb body, float strength, float damp)
    {
        if (body == null || body.Freeze) return;

        Vector3 currentUp = body.GlobalBasis.Y;
        Vector3 axis = currentUp.Cross(Vector3.Up);

        if (axis.LengthSquared() > 0.00001f)
        {
            float angle = currentUp.AngleTo(Vector3.Up);
            Vector3 torque = axis.Normalized() * angle * strength * body.Mass;
            float maxTorque = strength * body.Mass; // keep the solver from launching him into orbit
            if (torque.Length() > maxTorque) torque = torque.Normalized() * maxTorque;
            body.ApplyTorque(torque);
        }

        body.ApplyTorque(-body.AngularVelocity * damp * body.Mass);
    }


    private void Wander(float delta)
    {
        _wanderTimer -= delta;
        if (_wanderTimer <= 0.0f) NewWanderDirection();

        if (_moveDir.LengthSquared() < 0.0001f) return;

        Hips.ApplyCentralForce(_moveDir * _walkForce * Hips.Mass * 0.1f);
        Chest.ApplyCentralForce(_moveDir * _walkForce * Chest.Mass * 0.08f);

        // turn to face where he's going
        Vector3 forward = -Chest.GlobalBasis.Z;
        Vector3 flatForward = new Vector3(forward.X, 0, forward.Z).Normalized();
        float turn = flatForward.Cross(_moveDir).Y;
        Chest.ApplyTorque(Vector3.Up * turn * 6.0f * Chest.Mass);

        // clumsy leg shuffle
        float step = Mathf.Sin(_time * 6.0f);
        ApplyLegStep("UpperLegL", step);
        ApplyLegStep("UpperLegR", -step);
    }


    private void ApplyLegStep(string legName, float amount)
    {
        RagdollLimb leg = GetNodeOrNull<RagdollLimb>(legName);
        if (leg == null || leg.Freeze) return;

        leg.ApplyCentralForce(_moveDir * amount * 8.0f * leg.Mass);
        leg.ApplyCentralForce(Vector3.Up * Mathf.Max(amount, 0.0f) * 6.0f * leg.Mass);
    }


    private void Flail(float delta)
    {
        if (_flail <= 0.0f) return;

        ApplyFlail("UpperArmL", 0.0f);
        ApplyFlail("UpperArmR", 1.7f);
        ApplyFlail("LowerArmL", 3.1f);
        ApplyFlail("LowerArmR", 4.6f);
    }


    private void ApplyFlail(string limbName, float phase)
    {
        RagdollLimb limb = GetNodeOrNull<RagdollLimb>(limbName);
        if (limb == null || limb.Freeze) return;

        float wave = Mathf.Sin(_time * 3.0f + phase);
        float wave2 = Mathf.Cos(_time * 2.3f + phase);
        limb.ApplyTorque(new Vector3(wave, wave2 * 0.5f, wave2) * _flail * limb.Mass);
    }


    private float GroundDistance()
    {
        PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;
        if (space == null) return -1.0f;

        Vector3 from = Hips.GlobalPosition;
        Vector3 to = from + Vector3.Down * (_standHeight * 2.0f);

        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, to, 1);
        Godot.Collections.Array<Rid> exclude = new();
        foreach (Rid rid in _limbRids) exclude.Add(rid);
        query.Exclude = exclude;

        Godot.Collections.Dictionary result = space.IntersectRay(query);
        if (result.Count <= 0) return -1.0f;

        Vector3 point = (Vector3)result["position"];
        return from.Y - point.Y;
    }


    private void NewWanderDirection()
    {
        _wanderTimer = (float)GD.RandRange(_wanderTimeMin, _wanderTimeMax);

        if (GD.Randf() < 0.25f)
        {
            _moveDir = Vector3.Zero; // stand around looking confused
            return;
        }

        float angle = GD.Randf() * Mathf.Tau;
        _moveDir = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)).Normalized();
    }
    #endregion


    #region Public API:
    /// <summary> Turn the balance controller off for a moment, he just goes floppy. </summary>
    public void GoLimp(float seconds = 1.5f)
    {
        _limpTimer = Mathf.Max(_limpTimer, seconds);
    }


    /// <summary> Throw the whole guy in a direction (used by the Guy Spawner). </summary>
    public void Launch(Vector3 velocity)
    {
        foreach (RagdollLimb limb in Limbs)
        {
            if (!IsInstanceValid(limb) || limb.Freeze) continue;
            limb.LinearVelocity = velocity;
        }

        GoLimp(0.6f);
    }


    /// <summary> Point him somewhere at spawn time. </summary>
    public void FaceDirection(Vector3 dir)
    {
        dir = new Vector3(dir.X, 0, dir.Z);
        if (dir.LengthSquared() < 0.0001f) return;

        LookAt(GlobalPosition + dir.Normalized(), Vector3.Up);
    }


    public void OnLimbGrabbed(RagdollLimb limb)
    {
        _grabCount += 1;
        GoLimp(0.5f);
    }


    public void OnLimbDropped(RagdollLimb limb)
    {
        _grabCount = Mathf.Max(_grabCount - 1, 0);
        GoLimp(1.0f);
    }
    #endregion
}
