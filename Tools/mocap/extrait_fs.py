"""Extrait des clips FS (Blender) : poignets, rotations des mains, bassin, par image."""
import bpy, json, sys
from mathutils import Vector
R = "/home/user/UberBagarre/Assets/Fantacode Studios/Melee Combat System/Animations/Unarmed/Attacks and Reactions/"
CLIPS = ["Left Jab", "Right Cross", "Left Body Punch", "Right Body Punch", "Left Hook", "lefthook 5",
         "Right Hook", "Right Overhand", "Right Slap", "Uppercut", "Block"]
out = {}
for n in CLIPS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=R + n + ".fbx")
    arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
    act = arm.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    sc = bpy.context.scene
    mw = arm.matrix_world
    bones = arm.data.bones
    def rest(name): return mw @ bones[name].head_local
    toes = rest("Left_Toes") - rest("Left_Foot"); toes.z = 0; toes.normalize()
    arm_len = (rest("Left_UpperArm") - rest("Left_LowerArm")).length + (rest("Left_LowerArm") - rest("Left_Hand")).length
    frames = []
    pb = arm.pose.bones
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        row = {"hips": list(mw @ pb["Hips"].head)}
        for side in ("Left", "Right"):
            b = pb[side + "_Hand"]
            m = (mw @ b.matrix).to_3x3()
            row[side] = {"p": list(mw @ b.head), "m": [list(r) for r in m],
                         "k": list(mw @ pb[side + "_MiddleProximal"].head),
                         "i": list(mw @ pb[side + "_IndexProximal"].head),
                         "l": list(mw @ pb[side + "_PinkyProximal"].head),
                         "t": list(mw @ pb[side + "_ThumbProximal"].head),
                         "s": list(mw @ pb[side + "_UpperArm"].head)}
        row["eyes"] = list(((mw @ pb["Left_Eye"].head) + (mw @ pb["Right_Eye"].head)) * 0.5)
        row["shoulders"] = list(((mw @ pb["Left_UpperArm"].head) + (mw @ pb["Right_UpperArm"].head)) * 0.5)
        row["sl"] = list(mw @ pb["Left_UpperArm"].head); row["sr"] = list(mw @ pb["Right_UpperArm"].head)
        frames.append(row)
    fps = sc.render.fps / sc.render.fps_base
    out[n] = {"fps": fps, "forward": list(toes), "arm": arm_len, "frames": frames}
    print(n, len(frames), "fps", fps, "arm %.3f" % arm_len, "fwd", [round(x, 2) for x in toes], flush=True)
json.dump(out, open(sys.argv[-1], "w"))
