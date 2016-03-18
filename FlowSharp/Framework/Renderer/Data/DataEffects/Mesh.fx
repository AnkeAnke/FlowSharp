cbuffer Globals : register(b0)
{
	float4x4 view;
	float4x4 projection;
};

float3 color;
float thickness;

// For shaded heighmapped version.
float3 worldNormal;

Texture2D colormap;
float minMap;
float maxMap;
SamplerState LinSampler {
	filter = MIN_MAG_POINT_MIP_LINEAR;
	AddressU = Clamp;
	AddressV = Clamp;
};
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

struct VS_IN
{
	float3 pos : POSITION;
	float scalar : SCALAR;
};

// ~~~~~~~~~~~~~~~~~~~ Height Color Coded Mesh ~~~~~~~~~~~~~~~~~~~~~~~~ //

//struct GS_IN_H
//{
//	float4 pos : SV_POSITION;
//	float height : HEIGHT;
//};

struct PS_IN_H
{
	float4 pos : SV_POSITION;
	float height : HEIGHT;
};

PS_IN_H VS_Height(VS_IN input)
{
	PS_IN_H output = (PS_IN_H)0;

	output.pos = mul(projection, mul(view, float4(input.pos, 1)));
	output.height = 0.5;// (input.scalar - minMap) / (maxMap - minMap);
	//output.worldPos.z = 6;

	return output;
}

float4 PS_Height(PS_IN_H input) : SV_Target
{
	return float4(colormap.Sample(LinSampler, float2(input.height, 0.5)).xyz, 1.0); // input.color;
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

technique10 Height
{
	pass P0
	{
		SetVertexShader(CompileShader(vs_4_0, VS_Height()));
		SetGeometryShader(0);
		SetPixelShader(CompileShader(ps_4_0, PS_Height()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}
