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
	__constant__ unsigned int HalfNumNeighbors = 4;
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
		int numSteps = 1000;

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

				// Critical point?
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

	//// ~~~~~~~~~~~ FTLE Start Settings ~~~~~~~~~~~ //
	//__device__ float2 LoadAdvectFTLE(int2 origin)
	//{
	//	// Offset position by origin. Assume all blocks are in the seed range.
	//	int px = origin.x + blockIdx.x;
	//	int py = origin.y + blockIdx.y;
	//	float2 position = make_float2(px, py);


	//	return AdvectParticle(position);
	//}

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

	__global__ void CutNeighbors(float* neighborMap, float2* positions, unsigned int offset, unsigned int neighborOffset, unsigned int neighbor)
	{
			// Enough memory for this?
			__shared__ float2 reference[1024];

			// Adding an offset in case the reference is "left" or "below" the current point.
			int idx = blockIdx.x + blockIdx.y * Width + offset;
			int idxRef = idx + neighborOffset;
			idx *= blockDim.x;
			idxRef *= blockDim.x;

			reference[threadIdx.x] = positions[idxRef + threadIdx.x];
			__syncthreads();	
			
			// Here comes the hammer methode!
			float2 pos = positions[idx + threadIdx.x];
			float sum = 0;

			// Compare to each particle at the reference position.
			for (int ref = 0; ref < blockDim.x; ++ref)
			{
				float diffX = (reference[ref].x - pos.x);
				float diffY = (reference[ref].y - pos.y);
				// Is the reference particle within the same "cell"?
//				if (diffX*diffX < 0.5f && diffY*diffY < 0.5f)
//					sum++;
				sum += 1.0 / max(0.5, sqrt(diffX*diffX + diffY*diffY));
			}

			__syncthreads();

			// Use the same buffer we had before. Reference particles are not needed anymore.
			reference[threadIdx.x].y = (float)sum/blockDim.x;
			__syncthreads();

			// Reduce.
			for (int nextSize = blockDim.x / 2; nextSize > 0; nextSize /= 2)
			{
				if (threadIdx.x < nextSize)
					reference[threadIdx.x].y += reference[threadIdx.x + nextSize].y;
				__syncthreads();
			}

			// Write data to texture.
			if (threadIdx.x == 0)
			{
				//surf2Dwrite(0.1f, cuts, positions[idx].x * sizeof(float), positions[idx].y, cudaBoundaryModeTrap);
				//surf2Dwrite(reference[0].x / blockDim.x, cuts, blockIdx.x*sizeof(float), blockIdx.y, cudaBoundaryModeTrap);
				neighborMap[(blockIdx.x + blockIdx.y * Width + offset) * HalfNumNeighbors + neighbor] = reference[0].y / blockDim.x;
			}
	}

	__global__ void DeformationTensorFTLE (float* neighborMap, float2* positions, unsigned int offset, unsigned int neighborOffset, unsigned int neighbor)
	{
		if (threadIdx.x > 0)
			return;
		if (blockIdx.x == 0 || blockIdx.x == Width - 1 || blockIdx.y == 0 || blockIdx.y == Height - 1)
			return;

		int idx = blockIdx.x + blockIdx.y * Width;
		int idxP; int idxN;

//		reference[threadIdx.x] = positions[idxRef + threadIdx.x];
		switch (neighbor)
		{
			// Right - Left.
		case 0:
		case 2:
			idxP = idx + 1;
			idxN = idx - 1;
			break;
			// Up - Down.
		case 1:
		case 3:
			idxP = idx + Width;
			idxN = idx - Width;
			break;
		}
		// Write data to texture.
		float diff = neighbor < 2 ?
			// U derivative.
			positions[idxP].x - positions[idxN].x :
			// V derivative.
			positions[idxP].y - positions[idxN].y;
		//surf2Dwrite(0.1f, cuts, positions[idx].x * sizeof(float), positions[idx].y, cudaBoundaryModeTrap);
		//surf2Dwrite(reference[0].x / blockDim.x, cuts, blockIdx.x*sizeof(float), blockIdx.y, cudaBoundaryModeTrap);
		neighborMap[idx * HalfNumNeighbors + neighbor] = diff;	
	}

	__global__ void ScanStoreDensity(cudaSurfaceObject_t dens, float* neighborMap, unsigned int pad, int2 origin)
	{
			int px = blockIdx.x * blockDim.x + threadIdx.x + origin.x;
			int py = blockIdx.y * blockDim.y + threadIdx.y + origin.y;
			int linIdx = py * Width + px;
			linIdx *= HalfNumNeighbors;
			// Exclude outermost pixels.
			if (px > 0 && py > 0 && px < Width-1 && py < Height-1)
			{
				float density = 0;
				// Right.
				density += neighborMap[linIdx + 0];
				// Left.
				density += neighborMap[linIdx - HalfNumNeighbors + 0];

				// Up.
				density += neighborMap[linIdx + 1];
				// Down.
				density += neighborMap[linIdx - HalfNumNeighbors*Width + 1];

				// Upper Right.
				density += neighborMap[linIdx + 2];
				density += neighborMap[linIdx - HalfNumNeighbors*(Width + 1) + 2];

				// Upper Left.
				density += neighborMap[linIdx + 3];
				density += neighborMap[linIdx + HalfNumNeighbors*(1-Width) + 3];

				surf2Dwrite(density/8, dens, px * sizeof(float), py, cudaBoundaryModeTrap);
			}
	}

	__global__ void ScanStoreMin(cudaSurfaceObject_t mins, float* neighborMap, unsigned int pad, int2 origin)
	{
		int px = blockIdx.x * blockDim.x + threadIdx.x + origin.x;
		int py = blockIdx.y * blockDim.y + threadIdx.y + origin.y;
		int linIdx = py * Width + px;
		linIdx *= HalfNumNeighbors;
		// Exclude outermost pixels.
		if (px > 0 && py > 0 && px < Width - 1 && py < Height - 1)
		{
			float density = 1;
			// Right.
			density = min(neighborMap[linIdx + 0], density);
			// Left.
			density = min(neighborMap[linIdx - HalfNumNeighbors + 0], density);

			// Up.
			density = min(neighborMap[linIdx + 1], density);
			// Down.
			density = min(neighborMap[linIdx - HalfNumNeighbors*Width + 1], density);

			// Upper Right.
			density = min(neighborMap[linIdx + 2], density);
			density = min(neighborMap[linIdx - HalfNumNeighbors*(Width + 1) + 2], density);

			// Upper Left.
			density = min(neighborMap[linIdx + 3], density);
			density = min(neighborMap[linIdx + HalfNumNeighbors*(1 - Width) + 3], density);

			surf2Dwrite(density, mins, px * sizeof(float), py, cudaBoundaryModeTrap);
		}
	}

	__global__ void ScanStoreMax(cudaSurfaceObject_t maxs, float* neighborMap, unsigned int pad, int2 origin)
	{
		int px = blockIdx.x * blockDim.x + threadIdx.x + origin.x;
		int py = blockIdx.y * blockDim.y + threadIdx.y + origin.x;
		int linIdx = py * Width + px;
		linIdx *= HalfNumNeighbors;
		// Exclude outermost pixels.
		if (px > 0 && py > 0 && px < Width - 1 && py < Height - 1)
		{
			float density = 0;
			// Right.
			density = max(neighborMap[linIdx + 0], density);
			// Left.
			density = max(neighborMap[linIdx - HalfNumNeighbors + 0], density);

			// Up.
			density = max(neighborMap[linIdx + 1], density);
			// Down.
			density = max(neighborMap[linIdx - HalfNumNeighbors*Width + 1], density);

			// Upper Right.
			density = max(neighborMap[linIdx + 2], density);
			density = max(neighborMap[linIdx - HalfNumNeighbors*(Width + 1) + 2], density);

			// Upper Left.
			density = max(neighborMap[linIdx + 3], density);
			density = max(neighborMap[linIdx + HalfNumNeighbors*(1 - Width) + 3], density);

			surf2Dwrite(density, maxs, px * sizeof(float), py, cudaBoundaryModeTrap);
		}
	}

	__global__ void ScanStoreRange(cudaSurfaceObject_t diffs, float* neighborMap, unsigned int pad, int2 origin)
	{
		int px = blockIdx.x * blockDim.x + threadIdx.x + origin.x;
		int py = blockIdx.y * blockDim.y + threadIdx.y + origin.x;
		int linIdx = py * Width + px;
		linIdx *= HalfNumNeighbors;

		if (px > 0 && py > 0 && px < Width - 1 && py < Height - 1)
		{
			float minDens = 1;
			// Right.
			minDens = min(neighborMap[linIdx + 0], minDens);
			// Left.
			minDens = min(neighborMap[linIdx - HalfNumNeighbors + 0], minDens);

			// Up.
			minDens = min(neighborMap[linIdx + 1], minDens);
			// Down.
			minDens = min(neighborMap[linIdx - HalfNumNeighbors*Width + 1], minDens);

			// Upper Right.
			minDens = min(neighborMap[linIdx + 2], minDens);
			minDens = min(neighborMap[linIdx - HalfNumNeighbors*(Width + 1) + 2], minDens);

			// Upper Left.
			minDens = min(neighborMap[linIdx + 3], minDens);
			minDens = min(neighborMap[linIdx + HalfNumNeighbors*(1 - Width) + 3], minDens);

			float maxDens = 0;
			// Right.
			maxDens = max(neighborMap[linIdx + 0], maxDens);
			// Left.
			maxDens = max(neighborMap[linIdx - HalfNumNeighbors + 0], maxDens);

			// Up.
			maxDens = max(neighborMap[linIdx + 1], maxDens);
			// Down.
			maxDens = max(neighborMap[linIdx - HalfNumNeighbors*Width + 1], maxDens);

			// Upper Right.
			maxDens = max(neighborMap[linIdx + 2], maxDens);
			maxDens = max(neighborMap[linIdx - HalfNumNeighbors*(Width + 1) + 2], maxDens);

			// Upper Left.
			maxDens = max(neighborMap[linIdx + 3], maxDens);
			maxDens = max(neighborMap[linIdx + HalfNumNeighbors*(1 - Width) + 3], maxDens);

			surf2Dwrite(maxDens - minDens, diffs, px * sizeof(float), py, cudaBoundaryModeTrap);
		}
	}

	__global__ void ScanStoreDirection(cudaSurfaceObject_t map, float* neighborMap, unsigned int neighbor, int2 origin)
	{
		int px = blockIdx.x * blockDim.x + threadIdx.x + origin.x;
		int py = blockIdx.y * blockDim.y + threadIdx.y + origin.x;
		int linIdx = py * Width + px;
		linIdx *= HalfNumNeighbors;
		// Exclude outermost pixels.
		if (px > 0 && py > 0 && px < Width - 1 && py < Height - 1)
		{
			float density = 0;
			switch (neighbor)
			{
			case 0:
			case 4:
				// Right.
				density += neighborMap[linIdx + 0];
				// Left.
				density += neighborMap[linIdx - HalfNumNeighbors + 0];
				break;
			case 1:
			case 5:
				// Up.
				density += neighborMap[linIdx + 1];
				// Down.
				density += neighborMap[linIdx - HalfNumNeighbors*Width + 1];
				break;
			case 2:
			case 6:
				// Upper Right.
				density += neighborMap[linIdx + 2];
				density += neighborMap[linIdx - HalfNumNeighbors*(Width + 1) + 2];
				break;
			case 3:
			default:
				// Upper Left.
				density += neighborMap[linIdx + 3];
				density += neighborMap[linIdx + HalfNumNeighbors*(1 - Width) + 3];
				break;
			}
			surf2Dwrite(density, map, px * sizeof(float), py, cudaBoundaryModeTrap);
		}
	}

	__global__ void ScanStoreNeighbor(cudaSurfaceObject_t map, float* neighborMap, unsigned int neighbor, int2 origin)
	{
		int px = blockIdx.x * blockDim.x + threadIdx.x + origin.x;
		int py = blockIdx.y * blockDim.y + threadIdx.y + origin.x;
		int linIdx = py * Width + px;
		linIdx *= HalfNumNeighbors;
		// Exclude outermost pixels.
		if (px > 0 && py > 0 && px < Width - 1 && py < Height - 1)
		{
			float density = 0;
			switch (neighbor)
			{
			case 0:
				// Right.
				density = neighborMap[linIdx + 0];
				break;
			case 4:
				// Left.
				density = neighborMap[linIdx - HalfNumNeighbors + 0];
				break;
			case 1:
				// Up.
				density = neighborMap[linIdx + 1];
				break;
			case 5:
				// Down.
				density = neighborMap[linIdx - HalfNumNeighbors*Width + 1];
				break;
			case 2:
				// Upper Right.
				density = neighborMap[linIdx + 2];
				break;
			case 6:
				// Lower left.
				density = neighborMap[linIdx - HalfNumNeighbors*(Width + 1) + 2];
				break;
			case 3:
				// Upper Left.
				density = neighborMap[linIdx + 3];
				break;
			default:
				// Lower Right.
				density = neighborMap[linIdx + HalfNumNeighbors*(1 - Width) + 3];
				break;
			}
			surf2Dwrite(density, map, px * sizeof(float), py, cudaBoundaryModeTrap);
		}
	}

	__global__ void ScanStoreFTLE(cudaSurfaceObject_t map, float* neighborMap, unsigned int neighbor, int2 origin)
	{
		int px = blockIdx.x * blockDim.x + threadIdx.x;
		int py = blockIdx.y * blockDim.y + threadIdx.y;
		int linIdx = py * Width + px;
		linIdx *= HalfNumNeighbors;
		// Exclude outermost pixels.
		if (px > 0 && py > 0 && px < Width - 1 && py < Height - 1)
		{
			// Compute Eigenvalues.
			// Load 4 values.
			float Ux = neighborMap[linIdx + 0];
			float Uy = neighborMap[linIdx + 1];
			float Vx = neighborMap[linIdx + 2];
			float Vy = neighborMap[linIdx + 3];
			float a = Ux*Ux + Vx*Vx;
			float b = Ux*Uy + Vx*Vy;
			float d = Uy*Uy + Vy*Vy;

			// Helpers.
			float Th = (a - d) * 0.5f;
			float D = a * d - b * b;
			float root = Th * Th - D;

			root = max(0.0f, root);

			root = sqrt(root);
			float l0 = Th + root;
			float l1 = Th - root;

			float lambdaMax = max(0.000001, max(l0, l1));

			//float a = neighborMap[linIdx + 0];
			//float b = neighborMap[linIdx + 1];
			//float c = neighborMap[linIdx + 2];
			//float d = neighborMap[linIdx + 3];

			//a *= a;
			//float bc = b*b * c*c;
			//d *= d;

			//float root = a*a - 2 * a*d + 4 * bc + d*d;
			//root = sqrt(max(0.0, root));
			//float lambdaMax = (a + d) * 0.5;
			surf2Dwrite(logf(sqrt(lambdaMax)) / IntegrationLength, map, px * sizeof(float), py, cudaBoundaryModeTrap);
		}
	}
}