// Faith & Fief route lines (docs/MVP/32 §7, §8.4): ribbons draped on the ground along a run of tiles. UV is (distance along the line, side -1..1),
// UV2 the offset from the centre line (so the line can be widened with the camera's distance, like the trails), COLOR the colour and fade.
//   mode 0  dashed, static      the pilgrimage route
//   mode 1  moving pulses       disease spreading along a route or border, from the start of the line toward its end
//   mode 2  hazard stripes      quarantine in force
//   mode 3  solid, feathered    the gold edge under a route that ends at the Cathedral
shader_type spatial;
render_mode blend_mix, cull_disabled, unshaded, depth_draw_never;

uniform int mode = 0;
uniform float width_boost = 1.0;
uniform float dash = 0.14;
uniform float speed = 0.5;

varying float along;
varying float side;

void vertex() {
	VERTEX.xz += UV2 * (width_boost - 1.0);
	VERTEX.y += (width_boost - 1.0) * 0.012;
	along = UV.x;
	side = UV.y;
}

void fragment() {
	float edge = 1.0 - smoothstep(0.55, 1.0, abs(side));
	vec3 col = COLOR.rgb;
	float m = 1.0;
	if (mode == 0) {
		m = step(fract(along / dash), 0.62);
		edge = 1.0 - smoothstep(0.8, 1.0, abs(side));
	} else if (mode == 1) {
		float f = fract(along / (dash * 2.4) - TIME * speed);
		m = 0.22 + 0.78 * pow(1.0 - f, 3.0);
		edge = 1.0 - smoothstep(0.7, 1.0, abs(side));
	} else if (mode == 2) {
		float f = fract((along + side * 0.05) / dash);
		col = f < 0.5 ? COLOR.rgb : vec3(0.06, 0.045, 0.03);
		edge = 1.0 - smoothstep(0.88, 1.0, abs(side));
	}
	ALBEDO = col;
	ALPHA = COLOR.a * edge * m;
}
