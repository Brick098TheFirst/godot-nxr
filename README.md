# NXR 
### C# XR toolkit for Godot 4!
- Firearm system/behaviors
- Player system/behaviors
- Snap-zones
- Interactables

# Usage 
This repo contains a full project demoing the toolkit. Clone into a newly created project, build the solution, then enable the plugin in 'Project Settings->Addons->NXR' 

# Ragdoll Guys + Guy Spawner
Wobbly "Human Fall Flat / Gang Beasts" style ragdoll NPCs and a gun that shoots them.

- `addons/nxr/scripts/npc/RagdollGuy.cs` - builds a full active ragdoll from code (hips, chest,
  head, arms, legs, feet held together with cone-twist / hinge joints). A spring + upright torque
  balance controller keeps him standing, he drunkenly wanders around, flails, falls over when hit
  or grabbed, and clumsily gets back up.
- `addons/nxr/scripts/npc/RagdollLimb.cs` - every body part is an NXR `Interactable`, so hands can
  grab him by any limb and drag/throw him. Firearm rays call `hit()` on limbs and knock him down.
- `addons/nxr/scripts/npc/GuySpawner.cs` - firearm muzzle component. On `OnFire` it instances a guy
  in front of the barrel and launches him. Old guys get cleaned up past `_maxGuys`.

Scenes:
- `demo/npc/guy.tscn` - one guy.
- `demo/firearms/guy_spawner.tscn` - the "Guy Spawner" gun (infinite ammo, no menu, just guys).

The gun sits on the weapon wall in `demo/demo.tscn` next to the other firearms - grab it with grip
and pull the trigger.
