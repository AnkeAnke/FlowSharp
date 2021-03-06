﻿cbuffer Globals : register(b0)
{
	float4x4 view;
	float4x4 projection;
};

Texture2D colormap;
float minMap;
float maxMap;

SamplerState LinSampler {
	filter = MIN_MAG_MIP_LINEAR;
	AddressU = Clamp;
	AddressV = Clamp;
};

Texture2D field0;
Texture2D field1;
Texture2D field2;
SamplerState PointSampler {
	Filter = MIN_MAG_POINT_MIP_LINEAR;
	AddressU = Clamp;
	AddressV = Clamp;
};

static const int NUM_STEPS_LIC = 30;
static const float STEPSIZE = 0.5f;

float4x4 world;
float width;
float height;
float invalidNum;

float2 Sample(float2 pos)
{
	float2 texPos = float2(pos.x / width, pos.y / height);
	float v0 = field0.SampleLevel(LinSampler, texPos, 0.0).x;
	float v1 = field1.SampleLevel(LinSampler, texPos, 0.0).x;
	return float2(v0, v1);
}

float2 EulerStep(float2 pos, float scale)
{
	float2 vPos = Sample(pos);
	float vLen = length(vPos);
	float2 v = Sample(pos + scale * vPos/vLen);
	if (v.x == invalidNum && v.y == invalidNum)
		return pos;
	return pos + scale*vPos/vLen;
}

float SimpleRandomInt2(int2 iPos)
{
	return frac(sin(dot(float2(iPos), float2(12.9898, 78.233))) * 43758.5453);
}

float SimpleRandom(float2 pos)
{
	int2 iPos = int2(pos);
	float2 t = pos - float2(iPos);

	// Compute 4 corners.
	float val00 = SimpleRandomInt2(iPos);
	iPos.x++;
	float val10 = SimpleRandomInt2(iPos);
	iPos.y++;
	float val11 = SimpleRandomInt2(iPos);
	iPos.x--;
	float val01 = SimpleRandomInt2(iPos);
	return  (1 - t.y) * ((1 - t.x) * val00 + t.x * val10) +t.y * ((1 - t.x) * val01 + t.x * val11);
}

struct VS_IN
{
	float4 pos : POSITION;
	float4 uv : TEXTURE;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float4 uv : TEXTURE;
};

// ~~~~~~~~~~ Shaders ~~~~~~~~~~~ \\

PS_IN VS(VS_IN input)
{
	PS_IN output = (PS_IN)0;
	float4x4 mat;
	mat[0] = float4(1, 0, 0, 0); mat[1] = float4(0, 1, 0, 0); mat[2] = float4(0, 0, 1, 0); mat[3] = float4(0, 0, 0, 1);
	output.pos = mul(projection, mul(view, input.pos));
	//output.pos /= output.pos.w;
	output.uv = input.uv;// -float4(0.5 / width, 0.5 / height, 0, 0);

	return output;
}

PS_IN VS_zFight(VS_IN input)
{
	PS_IN output = (PS_IN)0;
	float4x4 mat;
	mat[0] = float4(1, 0, 0, 0); mat[1] = float4(0, 1, 0, 0); mat[2] = float4(0, 0, 1, 0); mat[3] = float4(0, 0, 0, 1);
	output.pos = mul(projection, mul(view, input.pos));
	float wOffset = 0.000005;
	output.pos.w += wOffset;
	//output.pos /= output.pos.w;
	output.uv = input.uv - float4(0.5 / width, 0.5 / height, 0, 0);

	return output;
}

// ~~~~~~~~~~ Colormap ~~~~~~~~~~ \\

float4 PS_nTex_1(PS_IN input) : SV_Target
{
	float value = field0.SampleLevel(PointSampler, input.uv.xy, 0.0).x;
	if (value == invalidNum)
		discard;
	value = saturate((value - minMap) / (maxMap - minMap));
	return colormap.Sample(LinSampler, float2(value, 0.5));
}

// ~~~~~ Value in 2 channels ~~~~~~ \\

float4 PS_nTex_2(PS_IN input) : SV_Target
{
	float v0 = field0.Sample(LinSampler, input.uv.xy, 0.0).x;
	float v1 = field1.Sample(LinSampler, input.uv.xy, 0.0).x;

	if (v0 == invalidNum && v1 == invalidNum)
		discard;

	v0 = saturate((v0 - minMap) / (maxMap - minMap));
	v1 = saturate((v1 - minMap) / (maxMap - minMap));

	return float4(v0, v1, 0.0, 1.0);
}

float4 PS_nTex_3(PS_IN input) : SV_Target
{
	float v0 = field0.Sample(LinSampler, input.uv.xy, 0.0).x;
	float v1 = field1.Sample(LinSampler, input.uv.xy, 0.0).x;
	float v2 = field2.Sample(LinSampler, input.uv.xy, 0.0).x;

	if (v0 == invalidNum && v1 == invalidNum)
	discard;

	v0 = saturate((v0 - minMap) / (maxMap - minMap));
	v1 = saturate((v1 - minMap) / (maxMap - minMap));
	v2 = saturate((v2 - minMap) / (maxMap - minMap));

	return float4(v0, v1, v2, 1.0);
}

float4 PS_diff_1(PS_IN input) : SV_Target
{
	float2 stepX = float2(1.0 / width, 0);
	float2 stepY = float2(0, 1.0 / height);
	float value = field0.SampleLevel(PointSampler, input.uv.xy, 0.0).x;
	value -= field0.SampleLevel(PointSampler, input.uv.xy + stepX, 0.0).x / 4;
	value -= field0.SampleLevel(PointSampler, input.uv.xy - stepX, 0.0).x / 4;
	value -= field0.SampleLevel(PointSampler, input.uv.xy + stepY, 0.0).x / 4;
	value -= field0.SampleLevel(PointSampler, input.uv.xy - stepY, 0.0).x / 4;

	if (value == invalidNum)
		discard;
	value = saturate((value - minMap) / (maxMap - minMap));
	return colormap.Sample(LinSampler, float2(value, 0.5));
}

float4 PS_grad_1(PS_IN input) : SV_Target
{
	float2 stepX = float2(1.0 / width, 0);
	float2 stepY = float2(0, 1.0 / height);
	float value = 0;
	float tmp = field0.SampleLevel(PointSampler, input.uv.xy + stepX, 0.0).x / 4;
	tmp -= field0.SampleLevel(PointSampler, input.uv.xy - stepX, 0.0).x / 4;
	value += tmp*tmp;
	tmp = field0.SampleLevel(PointSampler, input.uv.xy + stepY, 0.0).x / 4;
	tmp -= field0.SampleLevel(PointSampler, input.uv.xy - stepY, 0.0).x / 4;
	value += tmp*tmp;
	value = sqrt(value);

	if (value == invalidNum)
		discard;
	value = saturate((value - minMap) / (maxMap - minMap));
	return colormap.Sample(LinSampler, float2(value, 0.5));
}

// Overlay map. Useful for varying grid resolution overlays.
float4 PS_Alpha_1(PS_IN input) : SV_Target
{
	float value = field0.SampleLevel(PointSampler, input.uv.xy, 0.0).x;
	//if (value == invalidNum)
	//	discard;
	value = saturate((value - minMap) / (maxMap - minMap));

	return float4(colormap.Sample(LinSampler, float2(value, 0.5)).xyz, value);
}

// Whatever.
float4 PS_LIC_1(PS_IN input) : SV_Target
{
	float value = field0.SampleLevel(LinSampler, input.uv.xy, 0.0).x;
	if (value == invalidNum)
		discard;

	float4 noise = SimpleRandom(float2(input.uv.x * width, input.uv.y * height)).xxxx;

	noise.w = 1;
	return noise;
}

// Render a LIC image.
float4 PS_LIC_2(PS_IN input) : SV_Target
{
	// LIC. Move in both directions and add up random values there.
	float2 startPos = input.uv.xy;
	startPos.x *= width;
	startPos.y *= height;

	// Is the value valid?
	float2 vPos = Sample(startPos);
	if (vPos.x == invalidNum && vPos.y == invalidNum)
		discard;

	float sum = SimpleRandom(startPos);
	for (int sign = -1; sign <= 1; sign += 2)
	{
		float2 pos = startPos;

		for (int i = 0; i < NUM_STEPS_LIC; ++i)
		{
			pos = EulerStep(pos, (float(sign) * STEPSIZE));
			sum += SimpleRandom(pos) * smoothstep(1, NUM_STEPS_LIC, i);
		}
	}
	sum /= NUM_STEPS_LIC + 1;

	return float4(sum, sum, sum, 1.0);
}

float4 PS_LIC_3(PS_IN input) : SV_Target
{
	float4 LIC = PS_LIC_2(input);

	float value = field2.Sample(PointSampler, input.uv.xy).x;
	// Discarding should have been done by the LIC shader.

	value = saturate((value - minMap) / (maxMap - minMap));
	float4 scalar = colormap.Sample(LinSampler, float2(value, 0.5));

	return LIC * (1 - value) + scalar * value;
}

// A simple checkerboard to see the grid resolution.
float4 PS_Checker(PS_IN input) : SV_Target
{
	return ((int)(input.uv.x * width - 0.5) + (int)(input.uv.y * height - 0.5)) % 2 == 0 ? float4(0.8, 0.8, 0.8, 1.0) : float4(1.0, 1.0, 1.0, 1.0);
}

float4 PS_Checker_Tex_1(PS_IN input) : SV_Target
{
	float4 board = ((int)(input.uv.x * width) + (int)(input.uv.y * height)) % 2 == 0 ? float4(0.8, 0.8, 0.8, 1.0) : float4(1.0, 1.0, 1.0, 1.0);

	float value = field0.Sample(PointSampler, input.uv.xy).x;
	// Discarding should have been done by the LIC shader.

	value = saturate((value - minMap) / (maxMap - minMap));
	float4 scalar = colormap.Sample(LinSampler, float2(value, 0.5));

	return board * (1 - value) + scalar * value;
}

float4 PS_Checker_Tex_2(PS_IN input) : SV_Target
{
	return 0.5 * PS_Checker(input) + 0.5 *  PS_nTex_2(input);
}

float4 PS_Checker_Tex_3(PS_IN input) : SV_Target
{
	return 0.5 * PS_Checker(input) + 0.5 *  PS_nTex_3(input);
}

BlendState SrcAlphaBlendingAdd
{
	BlendEnable[0] = TRUE;
	SrcBlend = SRC_ALPHA;
	DestBlend = INV_SRC_ALPHA;
	BlendOp = ADD;
	SrcBlendAlpha = ONE;
	DestBlendAlpha = ONE;
	BlendOpAlpha = ADD;
	RenderTargetWriteMask[0] = 0x0F;
};

technique10 RenderTex1
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_nTex_1()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}

technique10 RenderTex2
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_nTex_2()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}

technique10 RenderTex3
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_nTex_3()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}

technique10 Laplace1
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_diff_1()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}

technique10 Gradient1
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_grad_1()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}

technique10 Overlay1
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS_zFight()));
		SetPixelShader(CompileShader(ps_4_0, PS_Alpha_1()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}

technique10 RenderLIC1
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_LIC_1()));
	}
}
technique10 RenderLIC2
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_LIC_2()));
	}
}
technique10 RenderLIC3
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_LIC_3()));
	}
}

technique10 RenderChecker
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_Checker()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}

technique10 RenderCheckerTex1
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_Checker_Tex_1()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}

technique10 RenderCheckerTex2
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_Checker_Tex_2()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}

technique10 RenderCheckerTex3
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_Checker_Tex_3()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}