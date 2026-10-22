// Worn trails: soft earth-coloured ribbons draped on the ground. UV carries each vertex's offset from the track's centre line, so
// the track can be widened with the camera's distance (a footpath would vanish at region zoom).
shader_type spatial;
render_mode blend_mix, cull_disabled, specular_disabled;

uniform float width_boost = 1.0;

void vertex() {
	VERTEX.xz += UV * (width_boost - 1.0);
	VERTEX.y += (width_boost - 1.0) * 0.012;
}

void fragment() {
	ALBEDO = COLOR.rgb;
	ALPHA = COLOR.a;
	ROUGHNESS = 1.0;
}
