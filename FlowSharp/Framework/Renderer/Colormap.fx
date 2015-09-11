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
	AddressU = Wrap;
	AddressV = Wrap;
};

struct VS_IN
{
	float4 pos : POSITION;
	float4 col : COLOR;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float4 col : COLOR;
};

PS_IN VS( VS_IN input )
{
	PS_IN output = (PS_IN)0;
	
	output.pos = input.pos;
	output.col = input.col;
	
	return output;
}

float4 PS_nTex_1( PS_IN input ) : SV_Target
{
	//return input.col;
	float value = field0.Sample(FieldTextureSampler, input.col).x;
	value = (value - 20.0) / 20.0;
	return colormap.Sample(ColormapSampler, float2(value, 0.5));
}

float4 PS_nTex_2(PS_IN input) : SV_Target
{
	//return float4(1.0, 1.0, 1.0, 2.0) - input.col;
	float v0 = field0.Sample(FieldTextureSampler, input.col).x;
	float v1 = field1.Sample(FieldTextureSampler, input.col).x;
	return float4(v0, v1, 0.0, 1.0);
}

technique10 RenderTex1
{
	pass P0
	{
		SetGeometryShader( 0 );
		SetVertexShader( CompileShader( vs_4_0, VS() ) );
		SetPixelShader( CompileShader( ps_4_0, PS_nTex_1() ) );
	}
}

technique10 RenderTex2
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS_nTex_2()));
	}
}