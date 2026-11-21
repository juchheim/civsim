// Discovery chart grain (docs/MVP/08 §3 and docs/MVP/39 §5): a subtle parchment tint, fine paper grain, a faint graticule and darker corners over the map at Z3 to Z4,
// as if the world were drawn on a navigator's chart. A screen overlay (a CanvasLayer under the HUD); `amount` 0 is off, 1 is the full effect, and the HUD decides when.
shader_type canvas_item;

uniform float amount : hint_range(0.0, 1.0) = 0.0;
uniform float grid_px = 160.0;
uniform vec3 parchment : source_color = vec3(0.93, 0.85, 0.66);

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

void fragment() {
	vec2 px = FRAGCOORD.xy;
	// paper grain: fine noise that changes slowly so it does not shimmer
	float grain = hash21(floor(px * 0.5));
	// the graticule: thin lines every grid_px pixels, a little heavier every fifth
	vec2 g = abs(fract(px / grid_px) - 0.5);
	float lines = 1.0 - smoothstep(0.0, 0.5 / grid_px * 1.6, min(g.x, g.y) - (0.5 - 0.5 / grid_px * 1.6));
	vec2 big = abs(fract(px / (grid_px * 5.0)) - 0.5);
	float major = 1.0 - smoothstep(0.0, 0.5 / (grid_px * 5.0) * 2.4, min(big.x, big.y) - (0.5 - 0.5 / (grid_px * 5.0) * 2.4));
	// corners a touch darker and warmer, like an aged sheet
	vec2 uv = SCREEN_UV - 0.5;
	float vig = smoothstep(0.35, 0.85, length(uv));
	float a = amount * (0.09 + 0.06 * grain + 0.11 * lines + 0.16 * major + 0.22 * vig);
	COLOR = vec4(mix(parchment, parchment * 0.55, vig), a);
}
