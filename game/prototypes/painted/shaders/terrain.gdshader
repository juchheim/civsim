shader_type spatial;
render_mode cull_back, diffuse_lambert, specular_disabled;

#include "res://shaders/common.gdshaderinc"

// baked relief: h, shore distance, mountain mask, trample
uniform sampler2D bake_tex : filter_linear, repeat_disable;
// per tile: biome, elevation, owner + 1, claim
uniform sampler2D tile_tex : filter_nearest, repeat_disable;
// per tile: flags low byte, plot kind + 1, unused, hash
uniform sampler2D tile_tex2 : filter_nearest, repeat_disable;
uniform sampler2D paper_tex : filter_linear_mipmap, repeat_enable;

uniform float res = 6.0;
uniform float margin = 10.0;
uniform vec2 bake_size = vec2(1024.0, 600.0);
uniform ivec2 map_tiles = ivec2(160, 100);

uniform vec4 biome_col[14];   // linear, season graded
uniform vec4 biome_col2[14];  // secondary pigment for blotches
uniform float biome_snow[14];
uniform vec4 civ_col[8];
uniform vec3 paper = vec3(0.86, 0.80, 0.66);
uniform vec3 ink = vec3(0.02, 0.014, 0.01);
uniform vec3 water_shallow = vec3(0.30, 0.52, 0.52);
uniform vec3 water_deep = vec3(0.16, 0.30, 0.40);
uniform vec3 rock_col = vec3(0.30, 0.26, 0.21);
uniform vec3 rock_col2 = vec3(0.40, 0.33, 0.25);
uniform vec3 earth_col = vec3(0.38, 0.25, 0.13);
uniform vec4 field_col_a = vec4(0.40, 0.48, 0.20, 1.0);
uniform vec4 field_col_b = vec4(0.62, 0.55, 0.25, 1.0);
uniform vec4 field_col_c = vec4(0.42, 0.32, 0.20, 1.0);
uniform vec3 sun_dir = vec3(0.5, 0.6, 0.5); // towards the sun, world space
uniform float snow_line = 0.75;
uniform float snow_amount = 0.0;
uniform float grid_on = 0.0;
uniform float territory_strength = 1.0;
uniform float hachure_strength = 1.0;
uniform float time_s = 0.0;
uniform vec2 world_size = vec2(160.5, 86.6);
uniform float light_wrap = 0.0; // > 0 softens the ramp (golden hour: grazing sun still warms the flats)
uniform vec3 snow_white = vec3(0.92, 0.94, 0.98);

// classic eight-point compass rose (signed coverage: 1 dark point, 2 light point, 3 ring)
float compass(vec2 q, out float ring) {
	float r = length(q);
	float a = atan(q.y, q.x);
	ring = 1.0 - smoothstep(0.0, 0.03, abs(r - 2.1)) ;
	ring = max(ring, 1.0 - smoothstep(0.0, 0.02, abs(r - 2.3)));
	float sector = a / (3.14159265 / 4.0);
	float k = floor(sector + 0.5);
	float local = (sector - k) * (3.14159265 / 4.0);
	float len = mod(k, 2.0) == 0.0 ? 3.2 : 1.9;
	float w = 0.42 * (1.0 - r / len);
	float inside = step(abs(local) * r, w) * step(r, len);
	float half_side = step(0.0, local);
	return inside * (1.0 + half_side);
}

varying vec3 wpos;
varying float v_cloud;

vec2 bake_uv(vec2 p) {
	return ((p + vec2(margin)) * res + vec2(0.5)) / bake_size;
}

float ground_h(vec2 p) {
	return max(texture(bake_tex, bake_uv(p)).r, 0.0);
}

void vertex() {
	vec3 w = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float h = textureLod(bake_tex, bake_uv(w.xz), 0.0).r;
	VERTEX.y = max(h, 0.0);
	wpos = vec3(w.x, VERTEX.y, w.z);
	float e = 1.0 / res;
	float hl = max(textureLod(bake_tex, bake_uv(w.xz - vec2(e, 0.0)), 0.0).r, 0.0);
	float hr = max(textureLod(bake_tex, bake_uv(w.xz + vec2(e, 0.0)), 0.0).r, 0.0);
	float hd = max(textureLod(bake_tex, bake_uv(w.xz - vec2(0.0, e)), 0.0).r, 0.0);
	float hu = max(textureLod(bake_tex, bake_uv(w.xz + vec2(0.0, e)), 0.0).r, 0.0);
	NORMAL = normalize(vec3(hl - hr, 2.0 * e, hd - hu));
}

vec4 tile0(ivec2 axial) {
	ivec2 o = axial_to_offset(axial);
	if (o.x < 0 || o.y < 0 || o.x >= map_tiles.x || o.y >= map_tiles.y) return vec4(0.0);
	return texelFetch(tile_tex, o, 0);
}

vec4 tile1(ivec2 axial) {
	ivec2 o = axial_to_offset(axial);
	if (o.x < 0 || o.y < 0 || o.x >= map_tiles.x || o.y >= map_tiles.y) return vec4(0.0);
	return texelFetch(tile_tex2, o, 0);
}

// large slow meander on top of the relief warp: biome edges wander like a brush, not a hex grid
vec2 paint_warp(vec2 p) {
	vec2 big = vec2(vnoise(p * 0.21 + vec2(1.7, 9.2)), vnoise(p * 0.21 + vec2(6.3, 2.8))) * 2.0 - 1.0;
	vec2 mid = vec2(vnoise(p * 0.55 + vec2(4.4, 0.6)), vnoise(p * 0.55 + vec2(0.9, 5.1))) * 2.0 - 1.0;
	return warp(p) + big * 0.9 + mid * 0.42;
}

void fragment() {
	vec2 p = wpos.xz;
	vec4 bk = texture(bake_tex, bake_uv(p));
	float h = bk.r;
	float shore = bk.g;
	float mount = bk.b;
	float trample = bk.a;

	// per-pixel normal from the bake (central differences of bilinear samples are continuous)
	float e = 1.0 / res;
	float hl = ground_h(p - vec2(e, 0.0));
	float hr = ground_h(p + vec2(e, 0.0));
	float hd = ground_h(p - vec2(0.0, e));
	float hu = ground_h(p + vec2(0.0, e));
	vec3 nw = normalize(vec3(hl - hr, 2.0 * e, hd - hu));
	float slope = 1.0 - nw.y;
	vec2 grad = vec2(hr - hl, hu - hd) / (2.0 * e);

	float gran = texture(paper_tex, p * 0.9).r;
	float gran2 = texture(paper_tex, p * 3.7 + vec2(0.3, 0.7)).r;
	float bloom = fbm3(p * 0.35 + vec2(4.0, 1.0));
	float bloom2 = fbm3(p * 1.4 + vec2(9.0, 3.0));

	vec3 col;
	float density = 1.0;
	float ink_amt = 0.0;
	float fw = fwidth(h);
	float v_snow = 0.0;

	if (h <= 0.0) {
		// ---- water: pale graded wash, darker band hugging the coast, hand-drawn ripple lines
		float depth = clamp(-h / 0.42, 0.0, 1.0);
		float deep = smoothstep(0.1, 0.95, depth);
		col = mix(water_shallow, water_deep, deep);
		density = 0.9 + 0.18 * (bloom - 0.5) + 0.12 * (gran - 0.5);
		float band = exp(-shore / 0.3);
		density += band * 0.28;
		float sw = shore + (vnoise(p * 1.7) - 0.5) * 0.08;
		float rip = 0.0;
		for (int k = 0; k < 3; k++) {
			float d0 = 0.17 + float(k) * 0.21 + float(k * k) * 0.035;
			float lw = fwidth(sw) * 0.75 + 0.005;
			float l = 1.0 - smoothstep(lw * 0.5, lw * 1.4, abs(sw - d0));
			float brk = smoothstep(0.30, 0.50, vnoise(p * vec2(2.2, 2.6) + float(k) * 7.0));
			rip = max(rip, l * brk * (1.0 - float(k) * 0.25));
		}
		ink_amt = rip * 0.40 * (1.0 - smoothstep(1.2, 1.6, shore));
		float far = smoothstep(1.5, 6.0, shore);
		density = mix(density, 0.8 + 0.25 * bloom, far);
		// compass rose in the open sea between the continents
		float ring;
		float cr = compass((p - vec2(80.25, 6.0)) / 1.45, ring);
		float aa_c = fwidth(p.x) * 1.5;
		if (cr > 0.5) {
			col = cr > 1.5 ? mix(col, vec3(0.75, 0.68, 0.52), 0.85) : mix(col, ink * 4.0, 0.75);
			density = 1.0;
		}
		ink_amt = max(ink_amt, ring * 0.7);
	} else {
		// ---- land
		vec2 q = paint_warp(p);
		ivec2 a0 = axial_round(q);
		vec2 c0 = axial_center(a0);
		float d00 = dot(q - c0, q - c0);
		vec3 acc = vec3(0.0);
		vec3 acc2 = vec3(0.0);
		float wsum = 0.0;
		float snowb = 0.0;
		float edge = 0.0;
		int b0 = int(tile0(a0).r * 255.0 + 0.5);
		if (b0 <= 1) b0 = 7;
		for (int i = 0; i < 7; i++) {
			ivec2 a = i == 0 ? a0 : a0 + hex_dir(i - 1);
			vec2 c = axial_center(a);
			float d2 = dot(q - c, q - c);
			float w = exp(-4.5 * (d2 - d00));
			int b = int(tile0(a).r * 255.0 + 0.5);
			if (b <= 1) b = 7; // shore under land: painted as the grassy strand
			acc += w * biome_col[b].rgb;
			acc2 += w * biome_col2[b].rgb;
			snowb += w * biome_snow[b];
			wsum += w;
			if (i > 0 && b != b0) edge += w;
		}
		vec3 pig = acc / wsum;
		vec3 pig2 = acc2 / wsum;
		snowb /= wsum;
		edge /= wsum;
		float mott = smoothstep(0.3, 0.75, fbm3(p * 0.6 + vec2(1.0, 7.0)) + (bloom2 - 0.5) * 0.6);
		col = mix(pig, pig2, mott);
		col = mix(col, col * vec3(1.03, 0.98, 0.9), smoothstep(0.15, 0.4, h) * 0.6);
		float fine = fbm3(p * 4.5 + vec2(3.0, 8.0));
		col *= 0.94 + 0.12 * fine;
		// gouache brush marks: short strokes whose direction wanders slowly across the land
		{
			float ang = vnoise(p * 0.35 + 7.0) * 3.14159;
			vec2 r = vec2(cos(ang) * p.x + sin(ang) * p.y, -sin(ang) * p.x + cos(ang) * p.y);
			float stroke = vnoise(r * vec2(2.5, 22.0)) - 0.5;
			float vis = 1.0 - smoothstep(0.02, 0.08, fwidth(r.y * 22.0) * 0.05);
			col *= 1.0 + stroke * 0.09 * vis;
		}
		col = mix(col, col * vec3(0.9, 1.02, 0.86), smoothstep(0.55, 0.8, fbm3(p * 2.2 + 11.0)) * 0.5);

		// mountains: warm rock on faces, inked ridge crests, snow above the line
		float mk = smoothstep(0.12, 0.6, mount);
		float rock = mk * smoothstep(0.05, 0.25, slope + mount * 0.15);
		vec3 rc = mix(rock_col, rock_col2, smoothstep(0.3, 0.7, bloom2 + nw.x * 0.3));
		col = mix(col, rc, clamp(rock, 0.0, 1.0));
		float sl = snow_line + (vnoise(p * 2.0) - 0.5) * 0.12;
		float snow = smoothstep(sl, sl + 0.05, h) * smoothstep(0.85, 0.45, slope);
		float sn = fbm3(p * 0.9) * 0.6 + fbm3(p * 5.0 + 3.0) * 0.3 + (gran - 0.5) * 0.12;
		float cover = snowb * snow_amount;
		snow = max(snow, smoothstep(0.64 - cover * 0.6, 0.69 - cover * 0.6, sn) * step(0.05, cover) * (1.0 - slope * 0.7));

		density = 0.95 + 0.30 * (bloom - 0.5) + 0.22 * (gran - 0.5) + 0.12 * (gran2 - 0.5);
		density += smoothstep(0.3, 0.5, edge) * 0.42;

		// curvature: ridge crests get a wobbly ink line
		float hc = ground_h(p);
		float lap = (hl + hr + hd + hu - 4.0 * hc) / (e * e);
		float ridge = smoothstep(-1.6, -4.5, lap + (vnoise(p * 4.0) - 0.5) * 1.5) * smoothstep(0.15, 0.5, mount);
		ink_amt = max(ink_amt, ridge * 0.75);

		// hachures: short downslope strokes in rows between contours, heavier on the shaded side
		float gl = length(grad);
		if (gl > 0.05) {
			vec2 dd = grad / gl;
			float u = dot(p, vec2(-dd.y, dd.x)) * 16.0 + vnoise(p * 3.0) * 0.8;
			float fu = abs(fract(u) - 0.5);
			float aw = fwidth(u);
			float stroke = 1.0 - smoothstep(0.10, 0.10 + aw * 1.2, fu);
			float rows = fract(h * 14.0 + vnoise(p * 2.0) * 0.3);
			stroke *= smoothstep(0.08, 0.16, rows) * smoothstep(0.92, 0.8, rows);
			float shade = 1.0 - clamp(dot(nw, normalize(sun_dir)) * 1.4, 0.0, 1.0);
			float amt = smoothstep(0.18, 0.55, slope) * (0.35 + 0.65 * shade) * (1.0 - smoothstep(0.25, 0.6, aw));
			ink_amt = max(ink_amt, stroke * amt * 0.55 * hachure_strength * (1.0 - snow * 0.7));
		}

		// grass ticks: little paired brush strokes, the old-map grassland symbol
		{
			vec2 gq = p * vec2(9.0, 11.0);
			vec2 gc = floor(gq);
			vec2 gf = fract(gq) - 0.5;
			float hh = hash12(gc + 0.5);
			vec2 jo = (vec2(hash12(gc + 3.3), hash12(gc + 7.7)) - 0.5) * 0.5;
			vec2 lq = gf - jo;
			float tick = 0.0;
			for (int k = -1; k <= 1; k += 2) {
				vec2 tq = lq - vec2(float(k) * 0.07, 0.0);
				tq.x += tq.y * 0.35 * float(k);
				float d = max(abs(tq.x) - 0.012, abs(tq.y) - 0.12);
				tick = max(tick, 1.0 - smoothstep(0.0, fwidth(gq.x) * 1.2, d));
			}
			int bb = b0;
			float gm = (bb == 7 || bb == 8 || bb == 6 || bb == 3) ? 1.0 : 0.0;
			gm *= step(0.62, hh) * smoothstep(0.5, 0.7, fbm3(p * 0.9 + 2.0));
			gm *= 1.0 - smoothstep(0.25, 0.5, fwidth(gq.x));
			density += tick * gm * 0.45;
			col = mix(col, col * vec3(0.82, 0.86, 0.7), tick * gm * 0.6);
		}

		// settlement ground: trodden earth
		float tr = smoothstep(0.05, 0.6, trample + (gran - 0.5) * 0.2);
		col = mix(col, earth_col * (0.85 + 0.3 * bloom2), tr * 0.8);

		// field plots: a Voronoi patchwork of fields seeded across plot tiles
		vec2 g = p / 0.42;
		vec2 gi = floor(g);
		float f1 = 9.0, f2 = 9.0;
		vec2 cell = vec2(0.0);
		vec2 cpos = vec2(0.0);
		for (int j = -1; j <= 1; j++) {
			for (int i = -1; i <= 1; i++) {
				vec2 o = gi + vec2(float(i), float(j));
				vec2 fp = o + vec2(hash12(o * 1.31 + 0.7), hash12(o * 2.17 + 3.1)) * 0.85 + 0.075;
				float d = length(g - fp);
				if (d < f1) { f2 = f1; f1 = d; cell = o; cpos = fp; }
				else if (d < f2) { f2 = d; }
			}
		}
		vec2 cw = cpos * 0.42;
		ivec2 af = axial_round(cw);
		int plot = int(tile1(af).g * 255.0 + 0.5) - 1;
		if (plot >= 0) {
			float ch = hash12(cell * 0.77 + 5.0);
			float ang = ch * 3.14159;
			vec2 lr = vec2(cos(ang) * p.x - sin(ang) * p.y, sin(ang) * p.x + cos(ang) * p.y);
			vec3 fc = ch < 0.4 ? field_col_a.rgb : (ch < 0.72 ? field_col_b.rgb : field_col_c.rgb);
			if (plot == 1) fc = mix(biome_col[7].rgb, field_col_a.rgb, 0.5);
			float fq = plot == 1 ? 9.0 : 26.0;
			float furrow = 0.5 + 0.5 * sin(lr.y * 6.2831 * fq);
			fc *= 0.92 + 0.1 * furrow;
			float be = (f2 - f1) * 0.42; // world distance to the field edge (approx)
			float fwe = fwidth(be);
			float inside = smoothstep(0.012, 0.012 + fwe * 1.5 + 0.004, be);
			col = mix(col, fc, inside * 0.85);
			density += (1.0 - inside) * 0.25;
			ink_amt = max(ink_amt, (1.0 - smoothstep(0.003, 0.003 + fwe * 1.2, be)) * 0.12);
			density += (1.0 - smoothstep(0.0, 0.05, be)) * 0.25 * inside;
		}

		v_snow = clamp(snow, 0.0, 1.0);
		density = mix(density, 0.8 + 0.2 * gran, clamp(snow, 0.0, 1.0));

		// ---- territory: translucent civ wash, strongest just inside the border, modulated by claim
		vec2 pt = p;
		ivec2 ta = axial_round(pt);
		vec4 tt = tile0(ta);
		int own = int(tt.b * 255.0 + 0.5) - 1;
		if (own >= 0) {
			vec2 tc = axial_center(ta);
			float dd0 = dot(pt - tc, pt - tc);
			float bd = 1.0;
			for (int i = 0; i < 6; i++) {
				ivec2 a = ta + hex_dir(i);
				int o2 = int(tile0(a).b * 255.0 + 0.5) - 1;
				if (o2 != own) {
					vec2 c = axial_center(a);
					bd = min(bd, (dot(pt - c, pt - c) - dd0) * 0.5);
				}
			}
			bd += (vnoise(p * 3.0) - 0.5) * 0.06;
			float clm = tt.a;
			float wash = (0.12 + 0.16 * clm) * (0.7 + 0.6 * bloom);
			wash += 0.7 * max(exp(-max(bd, 0.0) / 0.22) - 0.03, 0.0);
			wash *= territory_strength;
			vec3 cc = civ_col[own].rgb;
			col = mix(col, col * mix(vec3(1.0), cc * 1.6, 0.6), clamp(wash, 0.0, 0.85));
		}
	}

	// hand-wobbled ink coastline, drawn on both sides of the zero contour
	float cw2 = fw * 1.0 + 0.0012;
	float coast = 1.0 - smoothstep(cw2 * 0.6, cw2 * 1.6, abs(h + (vnoise(p * 5.0) - 0.5) * 0.004));
	ink_amt = max(ink_amt, coast * 0.85);

	if (grid_on > 0.5) {
		ivec2 ga = axial_round(p);
		vec2 gc = axial_center(ga);
		float gd0 = dot(p - gc, p - gc);
		float ge = 1.0;
		for (int i = 0; i < 6; i++) {
			vec2 c = axial_center(ga + hex_dir(i));
			ge = min(ge, (dot(p - c, p - c) - gd0) * 0.5);
		}
		float gw = fwidth(ge) * 1.0;
		ink_amt = max(ink_amt, (1.0 - smoothstep(gw * 0.5, gw * 1.5, ge)) * 0.35);
	}

	vec3 pc = glaze(paper, col, density);
	if (h > 0.0) {
		// snow sits on top of the wash and is whiter than the paper
		pc = mix(pc, snow_white * (0.9 + 0.1 * gran), v_snow);
	}
	pc = mix(pc, ink, ink_amt);
	// map sheet: beyond the world the paper is bare, framed by a ruled double neatline
	vec2 dmin = p - vec2(-1.5);
	vec2 dmax = world_size + vec2(1.5) - p;
	float edge_d = min(min(dmin.x, dmin.y), min(dmax.x, dmax.y));
	float fwp = fwidth(p.x);
	float frame = 1.0 - smoothstep(fwp * 0.8, fwp * 2.0, abs(edge_d));
	frame = max(frame, 1.0 - smoothstep(fwp * 0.5, fwp * 1.5, abs(edge_d + 0.35)));
	if (edge_d < 0.0) pc = paper * (0.97 + 0.04 * gran);
	// graduated border band between the two rules
	if (edge_d < 0.0 && edge_d > -0.35) {
		float tick = step(0.5, fract((dmin.x < 0.35 || dmax.x < 0.35 ? p.y : p.x) * 0.5));
		pc = mix(pc, ink, tick * 0.55);
	}
	pc = mix(pc, ink, frame * 0.9);
	ALBEDO = pc;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
	NORMAL = normalize((VIEW_MATRIX * vec4(nw, 0.0)).xyz);
	v_cloud = h > 0.0 ? cloud_shadow(p, time_s) : 1.0;
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float r = smoothstep(-0.05 - light_wrap, 0.75 - light_wrap * 0.5, ndl);
	DIFFUSE_LIGHT += LIGHT_COLOR * ATTENUATION * r * v_cloud / PI;
}
