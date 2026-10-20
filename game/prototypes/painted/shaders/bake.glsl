#[compute]
#version 450

// One-time GPU bake of the relief: soft-blended hex heights, ridged mountains, carved river valleys,
// mountain mask, settlement trample, and (pass 1) distance from water to the nearest shore.
// The noise and hex functions are mirrored in shaders/common.gdshaderinc so colours line up with relief.

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) restrict readonly buffer Tiles { uint v[]; } tiles;   // biome | elev<<8 | flags<<16
layout(set = 0, binding = 1, std430) restrict readonly buffer Rivers { vec4 v[]; } rivers; // segment x0 z0 x1 z1
layout(set = 0, binding = 2, std430) restrict readonly buffer Spots { vec4 v[]; } spots;   // x z radius strength
layout(set = 0, binding = 3, std430) restrict buffer Out { vec4 v[]; } outb;              // h, river_d, mount, trample
layout(set = 0, binding = 4, std430) restrict buffer TexOut { vec4 v[]; } texout;         // pass 1: h, shore_d, mount, trample
layout(set = 0, binding = 5, std430) restrict buffer PaintTile { int v[]; } painttile;     // tile under the painted (warped) colour

layout(push_constant, std430) uniform Params {
	float res;      // samples per world unit
	float margin;   // world units of ocean around the map
	int bw;         // bake width (pixels)
	int bh;         // bake height
	int tw;         // tiles wide
	int th;         // tiles high
	int n_rivers;
	int n_spots;
	int pass;
	int pad0;
	int pad1;
	int pad2;
} p;

const float ROW = 0.8660254;

float hash12(vec2 q) {
	vec3 p3 = fract(vec3(q.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 q) {
	vec2 i = floor(q);
	vec2 f = fract(q);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 q) {
	float s = 0.0, a = 0.5;
	for (int i = 0; i < 5; i++) { s += a * vnoise(q); q = q * 2.03 + vec2(17.1, 9.3); a *= 0.5; }
	return s;
}

float ridged(vec2 q) {
	float s = 0.0, a = 0.55, w = 1.0;
	for (int i = 0; i < 5; i++) {
		float n = 1.0 - abs(vnoise(q) * 2.0 - 1.0);
		n = n * n * w;
		w = clamp(n * 1.6, 0.0, 1.0);
		s += a * n;
		q = q * 2.07 + vec2(5.3, 11.9);
		a *= 0.5;
	}
	return s;
}

vec2 warp(vec2 q) {
	vec2 w = vec2(vnoise(q * 0.85 + vec2(3.1, 7.7)), vnoise(q * 0.85 + vec2(11.7, 1.3))) * 2.0 - 1.0;
	vec2 w2 = vec2(vnoise(q * 2.6 + vec2(5.5, 2.2)), vnoise(q * 2.6 + vec2(8.1, 13.4))) * 2.0 - 1.0;
	return q + w * 0.30 + w2 * 0.09;
}

// must match paint_warp() in terrain.gdshader
vec2 paint_warp(vec2 q) {
	vec2 big = vec2(vnoise(q * 0.21 + vec2(1.7, 9.2)), vnoise(q * 0.21 + vec2(6.3, 2.8))) * 2.0 - 1.0;
	vec2 mid = vec2(vnoise(q * 0.55 + vec2(4.4, 0.6)), vnoise(q * 0.55 + vec2(0.9, 5.1))) * 2.0 - 1.0;
	return warp(q) + big * 0.9 + mid * 0.42;
}

uint tile_word(ivec2 axial) {
	int r = axial.y;
	int col = axial.x + (r - (r & 1)) / 2;
	if (r < 0 || r >= p.th || col < 0 || col >= p.tw) return 0u; // ocean outside the map
	return tiles.v[r * p.tw + col];
}

vec2 axial_center(ivec2 a) {
	return vec2(float(a.x) + float(a.y) * 0.5 + 0.5, float(a.y) * ROW + 0.5 * ROW);
}

ivec2 axial_round(vec2 w) {
	float rf = (w.y - 0.5 * ROW) / ROW;
	float qf = (w.x - 0.5) - rf * 0.5;
	float x = qf, z = rf, y = -x - z;
	float rx = round(x), ry = round(y), rz = round(z);
	float dx = abs(rx - x), dy = abs(ry - y), dz = abs(rz - z);
	if (dx > dy && dx > dz) rx = -ry - rz;
	else if (dy > dz) ry = -rx - rz;
	else rz = -rx - ry;
	return ivec2(int(rx), int(rz));
}

float tile_height(uint word) {
	uint b = word & 255u;
	float e = float((word >> 8) & 255u);
	if (b == 0u) return -0.42;
	if (b == 1u) return -0.12;
	float h = 0.07 + clamp(e - 70.0, 0.0, 120.0) / 110.0 * 0.16;
	h += pow(clamp((e - 160.0) / 80.0, 0.0, 1.0), 1.25) * 0.18;
	return h;
}

const ivec2 DIRS[6] = ivec2[6](ivec2(1, 0), ivec2(1, -1), ivec2(0, -1), ivec2(-1, 0), ivec2(-1, 1), ivec2(0, 1));

float seg_dist(vec2 q, vec4 s) {
	vec2 a = s.xy, b = s.zw;
	vec2 ab = b - a;
	float t = clamp(dot(q - a, ab) / max(dot(ab, ab), 1e-6), 0.0, 1.0);
	return length(q - a - ab * t);
}

void main() {
	ivec2 id = ivec2(gl_GlobalInvocationID.xy);
	if (id.x >= p.bw || id.y >= p.bh) return;
	int idx = id.y * p.bw + id.x;
	vec2 wp = vec2(float(id.x), float(id.y)) / p.res - vec2(p.margin);

	if (p.pass == 1) {
		// distance from a water sample to the nearest land sample (in world units), capped
		vec4 o = outb.v[idx];
		if (o.x > 0.0) { texout.v[idx] = vec4(o.x, 0.0, o.z, o.w); return; }
		int R = int(p.res * 2.6);
		float best = float(R) * float(R);
		for (int dy = -R; dy <= R; dy++) {
			int yy = id.y + dy;
			if (yy < 0 || yy >= p.bh) continue;
			for (int dx = -R; dx <= R; dx++) {
				float d2 = float(dx * dx + dy * dy);
				if (d2 >= best) continue;
				int xx = id.x + dx;
				if (xx < 0 || xx >= p.bw) continue;
				if (outb.v[yy * p.bw + xx].x > 0.0) best = d2;
			}
		}
		texout.v[idx] = vec4(o.x, sqrt(best) / p.res, o.z, o.w);
		return;
	}

	{
		ivec2 pa0 = axial_round(paint_warp(wp));
		int r = pa0.y;
		int col = pa0.x + (r - (r & 1)) / 2;
		painttile.v[idx] = (r < 0 || r >= p.th || col < 0 || col >= p.tw) ? -1 : r * p.tw + col;
	}
	vec2 q = warp(wp);
	ivec2 a0 = axial_round(q);
	float wsum = 0.0, hsum = 0.0, msum = 0.0;
	float d00 = dot(q - axial_center(a0), q - axial_center(a0));
	for (int i = 0; i < 7; i++) {
		ivec2 a = i == 0 ? a0 : a0 + DIRS[i - 1];
		vec2 c = axial_center(a);
		float d2 = dot(q - c, q - c);
		float w = exp(-3.2 * (d2 - d00));
		uint word = tile_word(a);
		uint b = word & 255u;
		hsum += w * tile_height(word);
		msum += w * ((b == 12u || b == 13u) ? 1.0 : 0.0) * (b == 13u ? 1.35 : 1.0);
		wsum += w;
	}
	float h = hsum / wsum;
	float mount = msum / wsum;

	// rolling ground everywhere on land
	float land = smoothstep(0.0, 0.12, h);
	h += land * ((fbm(wp * 0.55) - 0.5) * 0.10 + (fbm(wp * 2.2 + 4.0) - 0.5) * 0.035);
	// mountains: distinct peaks with radiating spurs (two or three per mountain tile), the illustrated-map idiom
	float hm = 0.0;
	ivec2 pa = axial_round(wp);
	for (int dq = -2; dq <= 2; dq++) {
		for (int dr = -2; dr <= 2; dr++) {
			if (abs(dq + dr) > 2) continue;
			ivec2 a = pa + ivec2(dq, dr);
			uint word = tile_word(a);
			uint b = word & 255u;
			if (b != 12u && b != 13u) continue;
			vec2 c = axial_center(a);
			for (int k = 0; k < 3; k++) {
				vec2 seed = vec2(a) * 1.37 + vec2(float(k) * 7.1, float(k) * 3.3);
				if (k == 2 && hash12(seed + 9.0) < 0.5) continue;
				vec2 pc = c + (vec2(hash12(seed), hash12(seed + 1.7)) - 0.5) * 0.75;
				float big = b == 13u ? 1.0 : 0.0;
				float H = mix(0.32, 0.55, hash12(seed + 4.2)) + big * 0.25;
				float R = mix(0.55, 0.8, hash12(seed + 6.6)) + big * 0.15;
				vec2 d = wp - pc;
				float ang = atan(d.y, d.x);
				float spur = vnoise(vec2(ang * 1.9 + hash12(seed) * 20.0, 0.5)) * 0.55 + vnoise(vec2(ang * 4.3, 3.5)) * 0.25;
				float dist = length(d) * (0.75 + spur * 0.6);
				float t = clamp(1.0 - dist / R, 0.0, 1.0);
				float prof = H * pow(t, 1.45);
				// smooth union of peaks
				hm = max(hm, prof) + 0.12 * min(hm, prof) * smoothstep(0.0, 0.2, min(hm, prof));
			}
		}
	}
	float foot = smoothstep(0.02, 0.5, mount);
	h += foot * 0.06 * (ridged(wp * 1.3) - 0.3);
	h = max(h, h * 0.5 + hm * 0.55 + (ridged(wp * 3.1) - 0.4) * 0.04 * smoothstep(0.05, 0.3, hm));

	// carve rivers into soft valleys
	float rd = 99.0;
	for (int i = 0; i < p.n_rivers; i++) rd = min(rd, seg_dist(wp, rivers.v[i]));
	if (h > 0.0) {
		float carve = (1.0 - smoothstep(0.0, 0.55, rd));
		h = max(h - carve * 0.07 * (1.0 + mount), min(h, 0.015 + rd * 0.05));
	}

	float tr = 0.0;
	for (int i = 0; i < p.n_spots; i++) {
		vec4 s = spots.v[i];
		float d = length(wp - s.xy) / s.z;
		tr = max(tr, s.w * (1.0 - smoothstep(0.55, 1.0, d + (vnoise(wp * 6.0) - 0.5) * 0.35)));
	}
	// flatten the ground a little under settlements
	outb.v[idx] = vec4(h, rd, mount, tr);
}
