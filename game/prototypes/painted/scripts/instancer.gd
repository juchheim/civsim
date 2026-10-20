## Collects instances per (mesh, chunk) and emits chunked MultiMeshInstance3D nodes with the painterly
## prop material and an ink hull pass. Chunks keep frustum culling effective; far chunks swap to low meshes.
extends RefCounted

const Palette = preload("res://scripts/palette.gd")
const MeshLib = preload("res://scripts/mesh_lib.gd")

const CHUNK := 16.0

var groups := {} # key -> {mesh, lo, xf: Array, cu: Array, opts}
var materials := {}
var paper_tex: Texture2D


func add(mesh_key: String, xf: Transform3D, custom: Color, opts := {}) -> void:
	var cx := int(floor(xf.origin.x / CHUNK))
	var cz := int(floor(xf.origin.z / CHUNK))
	var key := "%s|%d|%d" % [mesh_key, cx, cz]
	if not groups.has(key):
		groups[key] = {"mesh": mesh_key, "xf": [], "cu": [], "opts": opts}
	groups[key].xf.append(xf)
	groups[key].cu.append(custom)


func _material(mesh_key: String, opts: Dictionary) -> ShaderMaterial:
	var mk := mesh_key + str(opts)
	if materials.has(mk):
		return materials[mk]
	var m := ShaderMaterial.new()
	m.shader = load("res://shaders/prop.gdshader")
	m.set_shader_parameter("paper_tex", paper_tex)
	m.set_shader_parameter("sway", opts.get("sway", 0.0))
	m.set_shader_parameter("flat_shade", opts.get("flat", 0.0))
	var o := ShaderMaterial.new()
	o.shader = load("res://shaders/outline.gdshader")
	o.set_shader_parameter("ink", Vector3(Palette.INK.srgb_to_linear().r, Palette.INK.srgb_to_linear().g, Palette.INK.srgb_to_linear().b))
	o.set_shader_parameter("width_px", opts.get("outline", 1.3))
	o.set_shader_parameter("size", opts.get("size", 0.1))
	o.set_shader_parameter("hull_center_y", opts.get("hull_y", -1.0))
	if not opts.get("no_hull", false):
		m.next_pass = o
	materials[mk] = m
	return m


func build(parent: Node3D) -> Array:
	var nodes := []
	for key in groups.keys():
		var g: Dictionary = groups[key]
		var opts: Dictionary = g.opts
		var lod: String = opts.get("lod", "")
		var variants := [[g.mesh, 0.0, opts.get("far", 0.0)]]
		if lod != "":
			variants = [[g.mesh, 0.0, opts.get("lod_dist", 30.0)], [lod, opts.get("lod_dist", 30.0), opts.get("far", 0.0)]]
		for v in variants:
			var mm := MultiMesh.new()
			mm.transform_format = MultiMesh.TRANSFORM_3D
			mm.use_custom_data = true
			mm.mesh = MeshLib.get_mesh(v[0])
			mm.instance_count = g.xf.size()
			for i in g.xf.size():
				mm.set_instance_transform(i, g.xf[i])
				mm.set_instance_custom_data(i, g.cu[i])
			var mi := MultiMeshInstance3D.new()
			mi.multimesh = mm
			# far LOD: no ink hull (sub-pixel there) and no shadow casting
			var far_lod: bool = v[1] > 0.0
			mi.material_override = _material(v[0], opts.merged({"no_hull": true}) if far_lod else opts)
			mi.visibility_range_begin = v[1]
			mi.visibility_range_end = v[2]
			mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if opts.get("shadow", true) and not far_lod else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			parent.add_child(mi)
			nodes.append(mi)
	return nodes


func set_param(mesh_prefix: String, pname: String, value) -> void:
	for k in materials.keys():
		if (k as String).begins_with(mesh_prefix):
			materials[k].set_shader_parameter(pname, value)


func set_all(pname: String, value) -> void:
	for m in materials.values():
		m.set_shader_parameter(pname, value)


func set_time(t: float) -> void:
	for m in materials.values():
		m.set_shader_parameter("time_s", t)
