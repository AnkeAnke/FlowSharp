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
	return input.col;
}

float4 PS_nTex_2(PS_IN input) : SV_Target
{
	return float4(1.0, 1.0, 1.0, 2.0) - input.col;
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