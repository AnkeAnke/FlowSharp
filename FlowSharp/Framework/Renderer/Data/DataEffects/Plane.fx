Texture2D colormap;
SamplerState ColormapSampler {
	filter = MIN_MAG_MIP_LINEAR;
	AddressU = Clamp;
	AddressV = Wrap;
};

Texture2D field0;
Texture2D field1;
SamplerState FieldTextureSampler {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Clamp;
	AddressV = Clamp;
};

static const int NUM_STEPS_LIC = 50;

cbuffer FieldConstants : register(c0)
{
	float width;
	float height;
	float invalidNum;
};

float2 Sample(float2 pos)
{
	float2 texPos = float2(pos.x / width, pos.y / height);
	float v0 = field0.SampleLevel(FieldTextureSampler, texPos, 0.0).x;
	float v1 = field1.SampleLevel(FieldTextureSampler, texPos, 0.0).x;
	return float2(v0, v1);
}

float2 EulerStep(float2 pos, float scale)
{
	float2 vPos = Sample(pos);
	float2 v = Sample(pos + vPos);
	if (v.x == invalidNum && v.y == invalidNum)
		return pos;
	return pos + scale*vPos;
}

float SimpleRandom(float2 pos)
{
	int2 iPos = int2(pos);
	return frac(sin(dot(float2(iPos), float2(12.9898, 78.233))) * 43758.5453); //max((1.0 - abs(180.0 - pos.x)), 0); //
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

PS_IN VS(VS_IN input)
{
	PS_IN output = (PS_IN)0;

	output.pos = input.pos;
	output.uv = input.uv;

	return output;
}

float4 PS_nTex_1(PS_IN input) : SV_Target
{
	float value = field0.SampleLevel(FieldTextureSampler, input.uv.xy, 0.0).x;
	if (value == invalidNum)
		return float4(0.4, 0.0, 0.0, 0.0);

	value = (value - 20.0) / 20.0;


	return colormap.Sample(ColormapSampler, float2(value, 0.5));
}

float4 PS_nTex_2(PS_IN input) : SV_Target
{
	float v0 = field0.Sample(FieldTextureSampler, input.uv.xy, 0.0).x;
	float v1 = field1.Sample(FieldTextureSampler, input.uv.xy, 0.0).x;
	if (v0 == invalidNum && v1 == invalidNum)
		return float4(0.0, 0.0, 0.0, 0.0);

	return float4(v0, v1, 0.0, 1.0);
}


// Render a LIC image.
float4 PS_LIC(PS_IN input) : SV_Target
{
	// LIC. Move in both directions and add up random values there.
	float2 startPos = input.uv.xy;
	startPos.x *= width;
	startPos.y *= height;

	float sum = SimpleRandom(startPos);
	for (int sign = -1; sign <= 1; sign += 2)
	{
		float2 pos = startPos;

		for (int i = 0; i < NUM_STEPS_LIC; ++i)
		{
			pos = EulerStep(pos, float(sign) * 3);
			sum += SimpleRandom(pos) * smoothstep(1, NUM_STEPS_LIC, i);
		}
	}
	sum /= NUM_STEPS_LIC + 1;

	// Is the value valid?
	float2 vPos = Sample(startPos);
	if (vPos.x == invalidNum && vPos.y == invalidNum)
		return float4(0.4, 0.0, 0.0, 0.0);
	return float4(sum, sum, sum, 1.0);
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

technique10 RenderLIC
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_LIC()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}