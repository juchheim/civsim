// Territory borders: UV2.x = strip (0 dark outline, 1 patterned line, 2 inner glow), UV2.y = civ slot.
// UV.x runs along the hex edge (0..1), UV.y across the strip (0 at the edge side).
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform float glow_strength = 1.0;
uniform float line_boost = 2.2;

float pattern(int civ, vec2 uv) {
	float u = uv.x;
	float v = uv.y;
	if (civ == 0) return 1.0;                                            // solid
	if (civ == 1) return step(fract(u * 1.0 + 0.2), 0.7);                 // long dash
	if (civ == 2) {                                                       // dots
		vec2 d = vec2((fract(u * 4.0) - 0.5) * 0.144, (v - 0.5) * 0.06);
		return 1.0 - smoothstep(0.022, 0.03, length(d));
	}
	if (civ == 3) {                                                       // dash-dot
		float f = fract(u * 1.5);
		return max(step(f, 0.45), step(abs(f - 0.72), 0.07));
	}
	if (civ == 4) return max(step(v, 0.32), step(0.68, v));              // double line
	if (civ == 5) {                                                       // zigzag
		float z = abs(fract(u * 4.0) - 0.5) * 2.0;
		return 1.0 - step(0.28, abs(v - (0.15 + 0.7 * z)));
	}
	if (civ == 6) return step(fract(u * 3.0), 0.55);                      // short dash
	float f = fract(u * 3.0);                                             // chain
	float ring = abs(length(vec2((f - 0.5) * 2.2, (v - 0.5) * 1.6)) - 0.5);
	return 1.0 - step(0.17, ring);
}

void fragment() {
	int kind = int(UV2.x + 0.5);
	int civ = int(UV2.y + 0.5);
	vec3 col = COLOR.rgb;
	float pulse = 0.85 + 0.15 * sin(TIME * 1.3 + UV.x * 0.5);
	if (kind == 0) {
		ALBEDO = col * 0.10;
		ALPHA = 0.8;
	} else if (kind == 1) {
		float m = pattern(civ, UV);
		ALBEDO = mix(col * 0.18, col * line_boost, m);
		ALPHA = mix(0.55, 1.0, m);
	} else {
		float a = pow(1.0 - UV.y, 1.6);
		ALBEDO = col * 1.2;
		ALPHA = a * 0.62 * glow_strength * pulse;
	}
}
