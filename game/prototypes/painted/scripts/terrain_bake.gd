## Runs shaders/bake.glsl on a local RenderingDevice and keeps the result on the CPU (for prop placement)
## and as a float texture (for the terrain shader).
extends RefCounted

const FrameData = preload("res://scripts/frame_data.gd")

const RES := 6.0 # samples per world unit (one tile column)
const MARGIN := 10.0 # ocean around the map, world units

var bw := 0
var bh := 0
var data := PackedFloat32Array() # 4 floats per sample: h, river distance, mountain mask, trample
var texture: ImageTexture
var paint_tile := PackedInt32Array() # tile index under the painted colour at each sample (-1 off map)


func bake(fd: FrameData, river_segments: PackedFloat32Array, spots: PackedFloat32Array) -> void:
	var t0 := Time.get_ticks_msec()
	var ws := fd.world_size()
	bw = int(ceil((ws.x + 2.0 * MARGIN) * RES)) + 1
	bh = int(ceil((ws.y + 2.0 * MARGIN) * RES)) + 1

	var rd := RenderingServer.create_local_rendering_device()
	var src := RDShaderSource.new()
	var text := FileAccess.get_file_as_string("res://shaders/bake.glsl").replace("#[compute]", "")
	src.source_compute = text
	var spirv := rd.shader_compile_spirv_from_source(src)
	if spirv.compile_error_compute != "":
		push_error(spirv.compile_error_compute)
		return
	var shader := rd.shader_create_from_spirv(spirv)
	var pipeline := rd.compute_pipeline_create(shader)

	var n := fd.width * fd.height
	var tiles := PackedInt32Array()
	tiles.resize(n)
	for i in n:
		tiles[i] = fd.biome[i] | (fd.elevation[i] << 8) | ((fd.flags[i] & 0xffff) << 16)
	if river_segments.is_empty():
		river_segments = PackedFloat32Array([-999, -999, -999, -999])
	if spots.is_empty():
		spots = PackedFloat32Array([-999, -999, 1, 0])

	var b_tiles := rd.storage_buffer_create(n * 4, tiles.to_byte_array())
	var b_riv := rd.storage_buffer_create(river_segments.size() * 4, river_segments.to_byte_array())
	var b_spots := rd.storage_buffer_create(spots.size() * 4, spots.to_byte_array())
	var b_out := rd.storage_buffer_create(bw * bh * 16)
	var b_texout := rd.storage_buffer_create(bw * bh * 16)
	var b_paint := rd.storage_buffer_create(bw * bh * 4)

	var uniforms := []
	var bufs := [b_tiles, b_riv, b_spots, b_out, b_texout, b_paint]
	for i in bufs.size():
		var u := RDUniform.new()
		u.uniform_type = RenderingDevice.UNIFORM_TYPE_STORAGE_BUFFER
		u.binding = i
		u.add_id(bufs[i])
		uniforms.append(u)
	var uset := rd.uniform_set_create(uniforms, shader, 0)

	for pass_i in 2:
		var pc := StreamPeerBuffer.new()
		pc.put_float(RES)
		pc.put_float(MARGIN)
		pc.put_32(bw)
		pc.put_32(bh)
		pc.put_32(fd.width)
		pc.put_32(fd.height)
		pc.put_32(river_segments.size() / 4)
		pc.put_32(spots.size() / 4)
		pc.put_32(pass_i)
		pc.put_32(0)
		pc.put_32(0)
		pc.put_32(0)
		var cl := rd.compute_list_begin()
		rd.compute_list_bind_compute_pipeline(cl, pipeline)
		rd.compute_list_bind_uniform_set(cl, uset, 0)
		rd.compute_list_set_push_constant(cl, pc.data_array, pc.data_array.size())
		rd.compute_list_dispatch(cl, int(ceil(bw / 16.0)), int(ceil(bh / 16.0)), 1)
		rd.compute_list_end()
		rd.submit()
		rd.sync()

	data = rd.buffer_get_data(b_out).to_float32_array()
	var texbytes := rd.buffer_get_data(b_texout)
	paint_tile = rd.buffer_get_data(b_paint).to_int32_array()
	for b in bufs:
		rd.free_rid(b)
	rd.free_rid(pipeline)
	rd.free_rid(shader)
	rd.free()

	# texture for the terrain shader: h, shore distance, mountain mask, trample
	var img := Image.create_from_data(bw, bh, false, Image.FORMAT_RGBAF, texbytes)
	texture = ImageTexture.create_from_image(img)
	var hmax := -9.0
	for i in range(0, data.size(), 4):
		hmax = maxf(hmax, data[i])
	print("bake %dx%d in %d ms, max height %.2f" % [bw, bh, Time.get_ticks_msec() - t0, hmax])


## World position (x, z) to fractional sample coordinates.
func _sample(p: Vector2, ch: int) -> float:
	var fx := clampf((p.x + MARGIN) * RES, 0.0, bw - 1.001)
	var fy := clampf((p.y + MARGIN) * RES, 0.0, bh - 1.001)
	var ix := int(fx)
	var iy := int(fy)
	var tx := fx - ix
	var ty := fy - iy
	var i00 := (iy * bw + ix) * 4 + ch
	var a := lerpf(data[i00], data[i00 + 4], tx)
	var b := lerpf(data[i00 + bw * 4], data[i00 + bw * 4 + 4], tx)
	return lerpf(a, b, ty)


## Displayed ground height (water is flattened to 0).
func height_at(p: Vector2) -> float:
	return maxf(_sample(p, 0), 0.0)


func raw_height(p: Vector2) -> float:
	return _sample(p, 0)


func river_dist(p: Vector2) -> float:
	return _sample(p, 1)


func mountain(p: Vector2) -> float:
	return _sample(p, 2)


func trample(p: Vector2) -> float:
	return _sample(p, 3)


func painted_tile(p: Vector2) -> int:
	var ix := clampi(roundi((p.x + MARGIN) * RES), 0, bw - 1)
	var iy := clampi(roundi((p.y + MARGIN) * RES), 0, bh - 1)
	return paint_tile[iy * bw + ix]


func normal_at(p: Vector2) -> Vector3:
	var e := 1.0 / RES
	var hx := height_at(p + Vector2(e, 0)) - height_at(p - Vector2(e, 0))
	var hz := height_at(p + Vector2(0, e)) - height_at(p - Vector2(0, e))
	return Vector3(-hx, 2.0 * e, -hz).normalized()
