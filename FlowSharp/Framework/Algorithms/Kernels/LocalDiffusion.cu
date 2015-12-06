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

	__device__ float RandomWang(unsigned int& seed)
	{
		return (float)(WangHash(seed) % 8388593) / 8388593.0;
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

		unsigned int rndSeed = 61 + seed.x + seed.y*WidthCells + particleIdx;

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
				rndSeed = WangHash(rndSeed);
				v.x += gauss.x;
				v.y += gauss.y;

				//// Critical point?
				float vLen = v.x*v.x + v.y*v.y + 1;
				vLen = sqrt(vLen);

				// Bring to step size.
				float3 cpy;// = pos;
				cpy.x = pos.x + v.x * StepSize / vLen;
				cpy.y = pos.y + v.y * StepSize / vLen;
				cpy.z = pos.z + StepSize / vLen;

				// Test the rounded position again. Valid?
				valid = tex2D(vX_t0, (int)(cpy.x + 0.5), (int)(cpy.y + 0.5));
				if (valid == Invalid)
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

		int idx = blockIdx.x + blockIdx.y * Width;
		idx *= blockDim.x;
		idx += threadIdx.x;
		// !beware! "size mangling" occuring.
		positions[idx].x = referenceMap[(int)(positions[idx].x * CellToSeedRatio + 0.5) + (int)(positions[idx].y * CellToSeedRatio + 0.5) * WidthCells]; //referenceMap[blockIdx.x*CellToSeedRatio + blockIdx.y * CellToSeedRatio*WidthCells];//

		__syncthreads();

		// Reduce.
		for (int nextSize = blockDim.x / 2; nextSize > 0; nextSize/=2)
		{
			if (threadIdx.x < nextSize)
				positions[idx].x += positions[idx + nextSize].x;
			__syncthreads();
		}

		// Write data to texture.
		if (threadIdx.x == 0)
		{
			//surf2Dwrite(0.1f, cuts, positions[idx].x * sizeof(float), positions[idx].y, cudaBoundaryModeTrap);
			surf2Dwrite(positions[idx].x/blockDim.x, cuts, blockIdx.x*sizeof(float), blockIdx.y, cudaBoundaryModeTrap);
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
}










//
////Includes for IntelliSense 
//#define _SIZE_T_DEFINED
//#ifndef __CUDACC__
//#define __CUDACC__
//#endif
//#ifndef __cplusplus
//#define __cplusplus
//#endif
//#include "cuda_runtime.h"
//#include "device_launch_parameters.h"
//#include <cuda.h>
//#include <device_launch_parameters.h>
//#include <texture_fetch_functions.h>
//#include "float.h"
//#include <builtin_types.h>
//#include <vector_functions.h>
//#include <cublas.h>
//#include <cusparse.h>
//
//texture<float, 2, cudaReadModeElementType> vX_t0;
//texture<float, 2, cudaReadModeElementType> vY_t0;
//texture<float, 2, cudaReadModeElementType> vX_t1;
//texture<float, 2, cudaReadModeElementType> vY_t1;
//texture<float, 2, cudaReadModeElementType> selectionMap;
//
//extern "C"  {
//	__constant__ float Variance = 1.0f;
//	__constant__ int Width = 200;
//	__constant__ int Height = 200;
//	__constant__ int NumParticles = 1024;
//	__constant__ float TimeInGrid = 15.0f / 2.59f;
//	__constant__ float IntegrationLength = 1.0f;
//	__constant__ float StepSize = 0.3f;
//	__constant__ float Invalid = 3600000000;
//	__constant__ int CellToSeedRatio = 10;
//	__device__ const float TWO_PI = 2.0f*3.14159265358979323846f;
//
//	__device__ unsigned int WangHash(unsigned int seed)
//	{
//		seed = (seed ^ 61) ^ (seed >> 16);
//		seed *= 9;
//		seed = seed ^ (seed >> 4);
//		seed *= 0x27d4eb2d;
//		seed = seed ^ (seed >> 15);
//		return seed;
//	}
//
//	__device__ unsigned int RandomUInt(unsigned int& seed)
//	{
//		// Xorshift32
//		seed ^= (seed << 13);
//		seed ^= (seed >> 17);
//		seed ^= (seed << 5);
//
//		return seed;
//	}
//
//	__device__ float Random(unsigned int& seed)
//	{
//		return float(RandomUInt(seed) % 8388593) / 8388593.0;
//	}
//
//	__device__ float RandomWang(unsigned int& seed)
//	{
//		return (float)(WangHash(seed) % 8388593) / 8388593.0;
//	}
//
//	__device__ float2 Random2(unsigned int& seed)
//	{
//		return make_float2(Random(seed), Random(seed));
//	}
//
//
//	// Take idx's as seedc.
//	__device__ float SimpleRandom(float rnd)
//	{
//		float idxDot = threadIdx.x * 12.9898f + blockIdx.x * 78.233f;
//		float val = sin(idxDot*rnd) * 43758.5453f;
//		return val - truncf(val);
//	}
//
//	__device__ float2 BoxMuller(unsigned int& seed)
//	{
//		float u1 = RandomWang(seed);// gauss.x;
//		seed = WangHash(seed);
//		float u2 = RandomWang(seed);// gauss.y;
//
//		float lnU = sqrt(-2.0f * log(u1)) * Variance;
//		float piU = TWO_PI * log(u2);
//		return make_float2(lnU * cos(piU), lnU * sin(piU));
//	}
//
//	__device__ float3 AdvectParticle(int2 seed)
//	{
//		// Take threadIdx as particleIdx;
//		int particleIdx = threadIdx.x;
//
//		float3 pos = make_float3(seed.x, seed.y, 0);
//		int numSteps = 100000;
//
//		float3 v = make_float3(0, 0, 0);
//		float valid;
//
//		unsigned int rndSeed = seed.x + seed.y + particleIdx;
//
//		// Should I even start integrating? Should never happen, though...
//		valid = tex2D(vX_t0, pos.x + 0.5, pos.y + 0.5);
//		// Works.
//		if (valid < Invalid)
//		{
//			// Step.
//			while (pos.z < IntegrationLength && numSteps-- > 0 && pos.x >= 0 && pos.y >= 0 && (int)(pos.x + 0.5) < Width && (int)(pos.y + 0.5) < Height)
//			{
//				float t = pos.z / TimeInGrid;
//
//				// t0
//				v.x = tex2D(vX_t0, pos.x, pos.y) * (1 - t);
//				v.y = tex2D(vY_t0, pos.x, pos.y) * (1 - t);
//				// t1
//				v.x += tex2D(vX_t1, pos.x, pos.y) * t;
//				v.y += tex2D(vY_t1, pos.x, pos.y) * t;
//				v.z = 1;
//
//				// Add diffusion.
//				float2 gauss = BoxMuller(rndSeed);
//				rndSeed = WangHash(rndSeed);
//				v.x += gauss.x;
//				v.y += gauss.y;
//
//				//// Critical point?
//				float vLen = v.x*v.x + v.y*v.y + 1;
//				vLen = sqrt(vLen);
//				//if (vLen < 0.00000001)
//				//{
//				//	break;
//				//}
//
//
//				// Bring to step size.
//				float3 cpy;// = pos;
//				cpy.x = pos.x + v.x * StepSize / vLen;
//				cpy.y = pos.y + v.y * StepSize / vLen;
//				cpy.z = pos.z + StepSize / vLen;
//
//				// Test the rounded position again. Valid?
//				valid = tex2D(vX_t0, (int)(cpy.x + 0.5), (int)(cpy.y + 0.5));
//				if (valid == Invalid)
//				{
//					break;
//				}
//				pos = cpy;
//			}
//		}
//		return pos;
//	}
//
//	__global__ void AdvectSelectionMap(float2* particles, int2 seed)
//	{
//		float3 newPos = AdvectParticle(seed);
//		//float2* writeTo = particles + (int)(newPos.x * CellToSeedRatio + 0.5) + (int)(newPos.y * CellToSeedRatio + 0.5) * Width * CellToSeedRatio;
//		//atomicAdd(writeTo, 1.0f / blockDim.x);
//		particles[threadIdx.x] = make_float2(newPos.x, newPos.y);
//	}
//
//	__global__ void WriteParticlesAtomic(float2* particles, float* data)
//	{
//		float2 pos = particles[threadIdx.x];
//		float* writeTo = data + (int)(pos.x * CellToSeedRatio + 0.5) + (int)(pos.y * CellToSeedRatio + 0.5) * Width * CellToSeedRatio;
//		atomicAdd(writeTo, 1.0f / blockDim.x);
//	}
//
//	// data == mapT1 from step.
//	// mapT1 == mapT0 bound as surface.
//	__global__ void CopySelectionMap(cudaSurfaceObject_t mapT1, float* data)
//	{
//		int px = blockIdx.x * blockDim.x + threadIdx.x;
//		int py = blockIdx.y * blockDim.y + threadIdx.y;
//		int linIdx = py * Width * CellToSeedRatio + px;
//		if (px < Width*CellToSeedRatio && py < Height*CellToSeedRatio)
//		{
//			surf2Dwrite(data[linIdx], mapT1, px*sizeof(float), py, cudaBoundaryModeTrap);
//			data[linIdx] = 0;
//		}
//	}
//
//	__global__ void AdvectAndCutSeeds(cudaSurfaceObject_t referenceTex, int2 origin)
//	{
//		extern __shared__ float cut[];
//		int px = blockIdx.x + origin.x;
//		int py = blockIdx.y + origin.y;
//
//		int2 pos = make_int2(px, py);
//		float3 newPos = AdvectParticle(pos);
//
//		cut[threadIdx.x] = tex2D(selectionMap, newPos.x*CellToSeedRatio, newPos.y*CellToSeedRatio);
//
//		int step = blockDim.x / 2;
//		__syncthreads();
//		for (int vals = step; vals > 0; vals /= 2)
//		{
//			if (threadIdx.x < vals)
//			{
//				int pair = threadIdx.x + vals;
//				cut[threadIdx.x] += cut[pair];
//			}
//			__syncthreads();
//		}
//		//mapT1[px + py * Width] = cut[0] / blockDim.x;
//		if (px < Width && py < Height)
//			surf2Dwrite(cut[0] / blockDim.x, referenceTex, px*sizeof(float), py, cudaBoundaryModeTrap);
//
//	}
//
//	//__global__ void CopyCutMap(cudaSurfaceObject_t mapT1, float* data)
//	//{
//	//	int px = blockIdx.x * blockDim.x + threadIdx.x;
//	//	int py = blockIdx.y * blockDim.y + threadIdx.y;
//	//	int linIdx = py * Width + px;
//	//	if (px < Width && py < Height)
//	//	{
//	//		surf2Dwrite(data[linIdx], mapT1, px*sizeof(float), py, cudaBoundaryModeTrap);
//	//		data[linIdx] = 0;
//	//	}
//	//}
//}