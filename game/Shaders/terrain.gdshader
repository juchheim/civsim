shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform sampler2D tile_state : filter_nearest, repeat_disable;
uniform int season = 0;
uniform float season_progress = 0.0;

varying vec2 state_uv;

vec3 civ_color(int civ) {
	if (civ == 0) return vec3(0.90, 0.40, 0.02);
	if (civ == 1) return vec3(0.08, 0.56, 0.78);
	if (civ == 2) return vec3(0.00, 0.55, 0.37);
	if (civ == 3) return vec3(0.92, 0.78, 0.02);
	if (civ == 4) return vec3(0.04, 0.40, 0.68);
	if (civ == 5) return vec3(0.78, 0.27, 0.00);
	if (civ == 6) return vec3(0.67, 0.34, 0.55);
	return vec3(0.30, 0.30, 0.30);
}

void vertex() {
	state_uv = UV;
}

void fragment() {
	vec3 base = COLOR.rgb;
	vec4 state = texture(tile_state, state_uv);
	int owner = int(round(state.r * 255.0)) - 1;
	float claim = state.g;
	float visibility = state.b;

	vec3 spring = base * vec3(0.91, 1.08, 0.88);
	vec3 summer = base * vec3(1.04, 1.03, 0.88);
	vec3 autumn = base * vec3(1.12, 0.83, 0.65);
	vec3 winter = mix(base, vec3(0.72, 0.79, 0.84), 0.58);
	vec3 seasonal = spring;
	if (season == 1) seasonal = summer;
	if (season == 2) seasonal = autumn;
	if (season == 3) seasonal = winter;
	base = mix(base, seasonal, clamp(season_progress, 0.0, 1.0));
	if (owner >= 0) {
		float wash = 0.02 + min(claim, 1.0) * 0.07;
		base = mix(base, civ_color(owner), wash);
	}
	if (visibility < 0.25) {
		base = mix(base, vec3(0.055, 0.075, 0.095), 0.91);
	} else if (visibility < 0.75) {
		base = mix(base, vec3(0.18, 0.22, 0.25), 0.55);
	}
	ALBEDO = base;
	ROUGHNESS = 0.96;
	SPECULAR = 0.08;
}
