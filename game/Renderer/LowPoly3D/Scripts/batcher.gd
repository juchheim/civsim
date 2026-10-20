## Collects instances per (mesh, chunk) and turns them into MultiMeshInstance3D nodes.
class_name M7Batcher
extends RefCounted

var groups := {}
var instance_count := 0
var chunk_size := 20.0
var detail_chunk_size := 6.0

func add(mesh_name: String, xf: Transform3D, custom: Color, chunked: bool = true) -> void:
	var key := mesh_name
	if chunked:
		var cs := detail_chunk_size if mesh_name.begins_with("detail_") else chunk_size
		key += "@%d_%d" % [int(xf.origin.x / cs), int(xf.origin.z / (cs * M7WorldData.S))]
	var g = groups.get(key)
	if g == null:
		g = {"mesh": mesh_name, "count": 0, "buffer": PackedFloat32Array()}
		groups[key] = g
	var b: Basis = xf.basis
	var o: Vector3 = xf.origin
	var buffer: PackedFloat32Array = g.buffer
	buffer.append(b.x.x); buffer.append(b.y.x); buffer.append(b.z.x); buffer.append(o.x)
	buffer.append(b.x.y); buffer.append(b.y.y); buffer.append(b.z.y); buffer.append(o.y)
	buffer.append(b.x.z); buffer.append(b.y.z); buffer.append(b.z.z); buffer.append(o.z)
	buffer.append(custom.r); buffer.append(custom.g); buffer.append(custom.b); buffer.append(custom.a)
	g.count += 1

func build(parent: Node3D, kit: M7MeshKit, material_for: Callable) -> int:
	var updated := update_existing(parent, kit, material_for, {})
	return instance_count

## Reuses existing MultiMesh nodes across MatchFrame publications. Per-chunk instances
## keep distant actors and settlement props out of close-up draws.
func update_existing(parent: Node3D, kit: M7MeshKit, material_for: Callable, previous: Dictionary) -> Dictionary:
	var active := {}
	instance_count = 0
	var keys := groups.keys()
	keys.sort()
	for key in keys:
		var g = groups[key]
		var node: MultiMeshInstance3D = previous.get(key)
		var mm: MultiMesh
		if is_instance_valid(node):
			mm = node.multimesh
		else:
			mm = MultiMesh.new()
			mm.transform_format = MultiMesh.TRANSFORM_3D
			mm.use_custom_data = true
			mm.mesh = kit.get_mesh(String(g.mesh).trim_prefix("detail_"))
			mm.instance_count = 0
			node = MultiMeshInstance3D.new()
			node.name = String(key).replace("@", "_")
			node.multimesh = mm
			node.material_override = material_for.call(g.mesh)
			_configure_node(node, String(g.mesh))
			parent.add_child(node)
		mm.instance_count = int(g.count)
		mm.buffer = g.buffer
		active[key] = node
		instance_count += int(g.count)
	for key in previous:
		if not active.has(key) and is_instance_valid(previous[key]):
			previous[key].queue_free()
	if OS.get_cmdline_user_args().has("--tris"):
		var per := {}
		for key in groups.keys():
			var g = groups[key]
			var m: String = String(g.mesh).trim_prefix("detail_")
			var tris: int = kit.get_mesh(m).surface_get_array_len(0) / 3
			per[m] = per.get(m, 0) + tris * int(g.count)
		var ks := per.keys()
		ks.sort_custom(func(x, y): return per[x] > per[y])
		for k in ks.slice(0, 15):
			print("tris ", k, " ", per[k])
	return active

func _configure_node(node: MultiMeshInstance3D, mesh_name: String) -> void:
	if mesh_name.begins_with("tuft") or mesh_name.begins_with("tallgrass") or mesh_name == "reeds" \
		or mesh_name == "shadow_disc" or mesh_name == "field" or mesh_name == "flame" or mesh_name.begins_with("banner"):
		node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	if mesh_name.begins_with("tuft") or mesh_name.begins_with("tallgrass") or mesh_name == "reeds" \
		or mesh_name.begins_with("rock") or mesh_name == "bush" or mesh_name == "shrub":
		node.visibility_range_end = 48.0
		node.visibility_range_end_margin = 8.0
		node.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
	if mesh_name.begins_with("detail_"):
		node.visibility_range_end = 26.0
		node.visibility_range_end_margin = 5.0
		node.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
		node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
