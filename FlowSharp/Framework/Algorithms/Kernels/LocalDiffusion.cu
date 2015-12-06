//Includes for IntelliSense 
#define _SIZE_T_DEFINED
#ifndef __CUDACC__
#define __CUDACC__
#endif
#ifndef __cplusplus
#define __cplusplus
#endif
#include "cuda_runtime.h"
#include "device_launch_parameters.h"
#include <cuda.h>
#include <device_launch_parameters.h>
#include <texture_fetch_functions.h>
#include "float.h"
#include <builtin_types.h>
#include <vector_functions.h>
#include <cublas.h>
#include <cusparse.h>

texture<float, 2, cudaReadModeElementType> vX_t0;
texture<float, 2, cudaReadModeElementType> vY_t0;
texture<float, 2, cudaReadModeElementType> vX_t1;
texture<float, 2, cudaReadModeElementType> vY_t1;
//texture<float, 2, cudaReadModeElementType> referenceMap;

extern "C"  {
	__constant__ float Variance = 1.0f;
	// Change those two depending on cut or reference execution.
	__constant__ int Width = 200;
	__constant__ int Height = 200;
	__constant__ int WidthCells = 200;
	__constant__ int HeightCells = 200;
	__constant__ int NumParticles = 1024;
	__constant__ float TimeInGrid = 15.0f / 2.59f;
	__constant__ float IntegrationLength = 1.0f;
	__constant__ float StepSize = 0.3f;
	__constant__ float Invalid = 3600000000;
	__constant__ int CellToSeedRatio = 10;
	__device__ const float TWO_PI = 2.0f*3.14159265358979323846f;

	// ~~~~~~~~~~~~~~~~~~~~ Random Functions ~~~~~~~~~~~~~~~~~~~~ //
	__device__ unsigned int WangHash(unsigned int seed)
	{
		seed = (seed ^ 61) ^ (seed >> 16);
		seed *= 9;
		seed = seed ^ (seed >> 4);
		seed *= 0x27d4eb2d;
		seed = seed ^ (seed >> 15);
		return seed;
	}

	__device__ unsigned int Xorshift(unsigned int seed)
	{
		seed = seed ^ (seed << 13); 
		seed = seed ^ (seed >> 17); 
		seed = seed ^ (seed << 5);

		return seed;
	}

	__device__ float RandomWang(unsigned int& seed)
	{
		return (float)(WangHash(seed) % 8388593 + 1) / 8388594.0;
	}

	__device__ float2 BoxMuller(unsigned int& seed)
	{
		float u1 = RandomWang(seed);
		seed = WangHash(seed);
		float u2 = RandomWang(seed);

		float lnU = sqrt(-2.0f * log(u1)) * Variance;
		float piU = TWO_PI * log(u2);
		return make_float2(lnU * cos(piU), lnU * sin(piU));
	}

	// ~~~~~~~~~~~~~~ Particle Advection ~~~~~~~~~~~~~~ //
	__device__ float2 AdvectParticle(float2 seed)
	{
		// Take threadIdx as particleIdx;
		int particleIdx = threadIdx.x;

		float3 pos = make_float3(seed.x, seed.y, 0);
		int numSteps = 100000;

		float3 v = make_float3(0, 0, 0);
		float valid;

		unsigned int rndSeed = seed.x + seed.y*WidthCells + particleIdx;

		// Should I even start integrating? Should never happen, though...
		valid = tex2D(vX_t0, pos.x + 0.5, pos.y + 0.5);
		// Works.
		if (valid < Invalid)
		{
			// Step.
			while (pos.z < IntegrationLength && numSteps-- > 0 && pos.x >= 0 && pos.y >= 0 && (int)(pos.x + 0.5) < Width && (int)(pos.y + 0.5) < Height)
			{
				float t = pos.z / TimeInGrid;

				// t0
				v.x = tex2D(vX_t0, pos.x, pos.y) * (1 - t);
				v.y = tex2D(vY_t0, pos.x, pos.y) * (1 - t);
				// t1
				v.x += tex2D(vX_t1, pos.x, pos.y) * t;
				v.y += tex2D(vY_t1, pos.x, pos.y) * t;
				v.z = 1;

				// Add diffusion.
				float2 gauss = BoxMuller(rndSeed);
				rndSeed = Xorshift(rndSeed);
				v.x += gauss.x;
				v.y += gauss.y;

				//// Critical point?
				float vLen = v.x*v.x + v.y*v.y + 1;
				vLen = sqrt(vLen);

				// Bring to step size.
				float3 cpy;// = pos;
				float stride = min(StepSize/vLen, IntegrationLength - pos.z);
				cpy.x = pos.x + v.x * stride;
				cpy.y = pos.y + v.y * stride;
				cpy.z = pos.z + stride;

				// Test the rounded position again. Valid?
				valid = tex2D(vX_t0, (int)(cpy.x + 0.5), (int)(cpy.y + 0.5));
				if (valid == Invalid || cpy.x < 0 || cpy.y < 0 || (int)(cpy.x + 0.5)>= Width || (int)(pos.y + 0.5) >= Height)
				{
					break;
				}
				pos = cpy;
			}
		}
		return make_float2(pos.x, pos.y);
	}


	// Start integrating from a common seed. 
	// For cut particles: Origin should be (0,0) if all blocks can be computed in parallel.
	// For reference particles: Origin is seed point, scaled by ratio.
	__device__ float2 LoadAdvect(int2 origin)
	{
		// Offset position by origin. Assume all blocks are in the seed range.
		int px = origin.x + blockIdx.x;
		int py = origin.y + blockIdx.y;
		float2 position = make_float2(px, py);

		return AdvectParticle(position);
	}

	// ~~~~~~~~~~~~ Start Reference Particle Integration ~~~~~~~~~~~~ //
	__global__ void LoadAdvectReference(float2* positions, int2 seed)
	{
		positions[threadIdx.x] = LoadAdvect(seed);
	}

	// ~~~~~~~~~~~~ Start Cut Particle Integration ~~~~~~~~~~~~ //
	__global__ void LoadAdvectCut(float2* positions, int2 origin)
	{
		int2 pos = make_int2(origin.x + blockIdx.x, origin.y + blockIdx.y);
		positions[threadIdx.x + (pos.x + pos.y * Width) * blockDim.x] = LoadAdvect(origin);
	}

	// ~~~~~~~~~~~~ Advect Cut Particle ~~~~~~~~~~~~ //
	__global__ void AdvectCut(float2* positions, int2 origin)
	{
		int idx = origin.x + blockIdx.x + (origin.y + blockIdx.y) * Width;
		idx *= blockDim.x;
		idx += threadIdx.x;
		positions[idx] = AdvectParticle(positions[idx]);
	}

	// ~~~~~~~~~~~~ Advect Reference Particle ~~~~~~~~~~~~ //
	__global__ void AdvectReference(float2* positions)
	{
		positions[threadIdx.x] = AdvectParticle(positions[threadIdx.x]);
	}

	// ~~~~~~~~~~~~ Advect Reference Particles into Array ~~~~~~~~~~~~ //
	__global__ void AdvectStoreReference(float2* positions, float* referenceMap)
	{
		float2 pos = AdvectParticle(positions[threadIdx.x]);
		float* dest = referenceMap + (int)(pos.x * CellToSeedRatio + 0.5) + (int)(pos.y * CellToSeedRatio + 0.5) * WidthCells;
		atomicAdd(dest, 1.0f / blockDim.x);
	}

	// ~~~~~~~~~~~~ Cutting all other Particles with Reference Map ~~~~~~~~~~~~~~ //
	__global__ void FetchSumStoreCut(cudaSurfaceObject_t cuts, float2* positions, float* referenceMap)
	{
		__shared__ float scan[1024];
		int idx = blockIdx.x + blockIdx.y * Width;
		idx *= blockDim.x;
		idx += threadIdx.x;
		// !beware! "size mangling" occuring.
		scan[threadIdx.x] = referenceMap[(int)(positions[idx].x * CellToSeedRatio + 0.5) + (int)(positions[idx].y * CellToSeedRatio + 0.5) * WidthCells]; //referenceMap[blockIdx.x*CellToSeedRatio + blockIdx.y * CellToSeedRatio*WidthCells];//

		__syncthreads();

		// Reduce.
		for (int nextSize = blockDim.x / 2; nextSize > 0; nextSize/=2)
		{
			if (threadIdx.x < nextSize)
				scan[threadIdx.x] += scan[threadIdx.x + nextSize];
			__syncthreads();
		}

		// Write data to texture.
		if (threadIdx.x == 0)
		{
			//surf2Dwrite(0.1f, cuts, positions[idx].x * sizeof(float), positions[idx].y, cudaBoundaryModeTrap);
			surf2Dwrite(scan[0] / blockDim.x, cuts, blockIdx.x*sizeof(float), blockIdx.y, cudaBoundaryModeTrap);
		}
	}

	// ~~~~~~~~~~~~ Copy the Array Data to Texture ~~~~~~~~~~~~ //
	__global__ void ReferenceToTexture(cudaSurfaceObject_t referenceTex, float* data)
	{
		int px = blockIdx.x * blockDim.x + threadIdx.x;
		int py = blockIdx.y * blockDim.y + threadIdx.y;
		int linIdx = py * WidthCells + px;
		if (px < WidthCells && py < HeightCells)
		{
			surf2Dwrite(data[linIdx], referenceTex, px*sizeof(float), py, cudaBoundaryModeTrap);
			data[linIdx] = 0;
		}
	}

	// Gradient Kernels
	// ~~~~~~~~ Cut with right Particle Cloud ~~~~~~~~ //
	__global__ void CutX(float* cuts, float2* positions)
	{
		// Enough memory for this?
		float2 reference[1024];
		int idx = blockIdx.x + blockIdx.y * Width;
		idx *= blockDim.x;
		int idxR = idx + blockDim.x;
		idx += threadIdx.x;

		reference[threadIdx.x] = positions[threadIdx.x];
		__syncthreads();
		
		
		// Here comes the hammer methode!
		float2 pos = positions[idx];
		unsigned int sum = 0;
		for (int ref = 0; ref < blockIdx.x; ++ref)
		{
			// Is the reference particle within the same "cell"?
			if (abs((pos.x - reference[idxR].x) * (pos.y - reference[idxR].y)) < 0.25f)
				sum++;
			//positions[idx].x = referenceMap[(int)(positions[idx].x * CellToSeedRatio + 0.5) + (int)(positions[idx].y * CellToSeedRatio + 0.5) * WidthCells]; //referenceMap[blockIdx.x*CellToSeedRatio + blockIdx.y * CellToSeedRatio*WidthCells];//
		}
		__syncthreads();
		// Use the same buffer we had before. Reference particles are not needed anymore.

		reference[threadIdx.x].x = (float)sum;
		__syncthreads();
		// Reduce.
		for (int nextSize = blockDim.x / 2; nextSize > 0; nextSize /= 2)
		{
			if (threadIdx.x < nextSize)
				reference[idx].x += reference[idx + nextSize].x;
			__syncthreads();
		}

		// Write data to texture.
		if (threadIdx.x == 0)
		{
			//surf2Dwrite(0.1f, cuts, positions[idx].x * sizeof(float), positions[idx].y, cudaBoundaryModeTrap);
			//surf2Dwrite(reference[0].x / blockDim.x, cuts, blockIdx.x*sizeof(float), blockIdx.y, cudaBoundaryModeTrap);
			cuts[blockIdx.x + blockIdx.y * (Width - 1)] = reference[0].x;
		}
	}
	// ~~~~~~~~ Cut with upper Particle Cloud ~~~~~~~~ //
	__global__ void CutY(float* cuts, float2* positions)
	{

	}

	__global__ void StoreXY(cudaSurfaceObject_t gradsX, cudaSurfaceObject_t gradsY, float* cutsX, float* cutsY)
	{
		int px = blockIdx.x * blockDim.x + threadIdx.x;
		int py = blockIdx.y * blockDim.y + threadIdx.y;
		int linIdx = py * Width + px;
		if (px < Width-1 && py < Height-1)
		{
			surf2Dwrite(cutsX[linIdx], gradsX, px*sizeof(float), py, cudaBoundaryModeTrap);
			surf2Dwrite(cutsY[linIdx], gradsY, px*sizeof(float), py, cudaBoundaryModeTrap);
		}
	}
}