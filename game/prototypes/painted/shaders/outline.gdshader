shader_type spatial;
// Inverted-hull ink outline with a width in pixels that thins for tiny, distant props
// so a forest seen from far reads as massed canopies, not as solid ink.
render_mode cull_front, unshaded, specular_disabled;

uniform vec3 ink = vec3(0.02, 0.015, 0.01);
uniform float width_px = 1.4;
uniform float size = 0.1;          // typical object radius (world units) to thin far outlines
uniform float hull_center_y = -1.0; // >= 0: push from this local point instead of along normals

void vertex() {
	vec4 vc = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	float dist = max(-vc.z, 0.01);
	float px_per_unit = VIEWPORT_SIZE.y * PROJECTION_MATRIX[1][1] / (2.0 * dist);
	float obj_px = size * px_per_unit;
	float w = width_px * clamp(obj_px / 14.0, 0.25, 1.0);
	vec3 dir_local = hull_center_y >= 0.0 ? normalize(VERTEX - vec3(0.0, hull_center_y, 0.0)) : NORMAL;
	vec3 dv = normalize(mat3(MODELVIEW_MATRIX) * dir_local);
	vec4 clip = PROJECTION_MATRIX * vc;
	vec2 sd = (PROJECTION_MATRIX * vec4(dv, 0.0)).xy;
	float sl = length(sd);
	if (sl > 1e-5) sd /= sl;
	clip.xy += sd * w * 2.0 / VIEWPORT_SIZE * clip.w;
	POSITION = clip;
}

void fragment() {
	ALBEDO = ink;
}
