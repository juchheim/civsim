// Faith & Fief outbreak (docs/MVP/32 §8.4): a sickly, slowly turning stain on the ground round a settlement where disease is raging, with a pulsing
// rim. UV runs -1..1 across the disc; COLOR carries the tint and the strength (alpha).
shader_type spatial;
render_mode blend_mix, cull_disabled, unshaded, depth_draw_never;

void fragment() {
	float r = length(UV);
	if (r > 1.0) {
		discard;
	}
	float ang = atan(UV.y, UV.x);
	float swirl = sin(ang * 3.0 + r * 9.0 - TIME * 0.7) * 0.5 + 0.5;
	float swirl2 = sin(ang * 5.0 - r * 6.0 + TIME * 0.45) * 0.5 + 0.5;
	float pulse = 0.78 + 0.22 * sin(TIME * 1.7);
	float body = (1.0 - smoothstep(0.4, 1.0, r)) * (0.42 + 0.4 * swirl + 0.2 * swirl2) * pulse;
	float rim = smoothstep(0.78, 0.9, r) * (1.0 - smoothstep(0.9, 1.0, r)) * (0.4 + 0.25 * sin(TIME * 1.7 + 1.0));
	ALBEDO = mix(COLOR.rgb, COLOR.rgb * vec3(1.15, 1.1, 0.7), swirl);
	ALPHA = clamp(body + rim, 0.0, 1.0) * COLOR.a;
}
