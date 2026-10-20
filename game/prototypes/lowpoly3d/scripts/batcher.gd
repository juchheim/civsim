## Collects instances per (mesh, chunk) and turns them into MultiMeshInstance3D nodes.
class_name Batcher
extends RefCounted

const CHUNK := 20.0

var groups := {}

func add(mesh_name: String, xf: Transform3D, custom: Color, chunked: bool = true) -> void:
	var key := mesh_name
	if chunked:
		var cs := 6.0 if mesh_name.begins_with("detail_") else CHUNK
		key += "@%d_%d" % [int(xf.origin.x / cs), int(xf.origin.z / (cs * WorldData.S))]
	var g = groups.get(key)
	if g == null:
		g = {"mesh": mesh_name, "xf": [], "cu": []}
		groups[key] = g
	g.xf.append(xf)
	g.cu.append(custom)

func build(parent: Node3D, kit: MeshKit, material_for: Callable) -> int:
	var total := 0
	var keys := groups.keys()
	keys.sort()
	for key in keys:
		var g = groups[key]
		var mm := MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.use_custom_data = true
		mm.mesh = kit.get_mesh(String(g.mesh).trim_prefix("detail_"))
		mm.instance_count = g.xf.size()
		for i in g.xf.size():
			mm.set_instance_transform(i, g.xf[i])
			mm.set_instance_custom_data(i, g.cu[i])
		var mmi := MultiMeshInstance3D.new()
		mmi.multimesh = mm
		mmi.name = key.replace("@", "_")
		mmi.material_override = material_for.call(g.mesh)
		var mname := String(g.mesh)
		if mname.begins_with("tuft") or mname.begins_with("tallgrass") or mname == "reeds" or mname == "shadow_disc" or mname == "field" or mname == "flame" or mname.begins_with("banner"):
			mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		if mname.begins_with("tuft") or mname.begins_with("tallgrass") or mname == "reeds" or mname.begins_with("rock") or mname == "bush" or mname == "shrub":
			mmi.visibility_range_end = 48.0
			mmi.visibility_range_end_margin = 8.0
			mmi.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
		if mname.begins_with("detail_"):
			mmi.visibility_range_end = 26.0
			mmi.visibility_range_end_margin = 5.0
			mmi.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
			mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		parent.add_child(mmi)
		total += g.xf.size()
	if OS.get_cmdline_user_args().has("--tris"):
		var per := {}
		for key in groups.keys():
			var g = groups[key]
			var m: String = String(g.mesh).trim_prefix("detail_")
			var tris: int = kit.get_mesh(m).surface_get_array_len(0) / 3
			per[m] = per.get(m, 0) + tris * g.xf.size()
		var ks := per.keys()
		ks.sort_custom(func(x, y): return per[x] > per[y])
		for k in ks.slice(0, 15):
			print("tris ", k, " ", per[k])
	return total
