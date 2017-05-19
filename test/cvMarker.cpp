//===========================================================================
//!
//!	@brief		同時実行ビジュアライザーマーカー
//!
//===========================================================================

#include "stdafx.h"

#include <mutex>
#include <thread>
#include <cvmarkersobj.h>
#include "cvMarker.h"

using namespace Concurrency::diagnostic;

namespace cvMarker
{

#if defined(CVMARKER_V2)

	const unsigned int MAX_DEPTH = 16;

	// {EDBC9DC2-0C50-48E4-88DF-65AA0D8ECE00}
	const GUID MARKER_GUID = { 0xedbc9dc2, 0xc50, 0x48e4,{ 0x88, 0xdf, 0x65, 0xaa, 0xd, 0x8e, 0xce, 0x00 } };
	static_assert(MAX_DEPTH <= 0xff, "255まで");

	PCV_MARKERSERIES	_series[MAX_DEPTH];
	PCV_PROVIDER		_provider[MAX_DEPTH];
	bool				_initialized;
	std::mutex			_criticalSection;

	struct MARKER_OBJECT
	{
		PCV_SPAN		 _spans[MAX_DEPTH];
		int				 _spanCount;
	};
	__declspec(thread) MARKER_OBJECT _cvMarkerObject;


	bool initialize()
	{
		if(!_initialized)
		{
			std::lock_guard<std::mutex> lock(_criticalSection);

			for(int i = 0; i < MAX_DEPTH; ++i)
			{
				auto guid = MARKER_GUID; guid.Data4[7] = (unsigned char)i;
				if(SUCCEEDED(CvInitProvider(&guid, &_provider[i])))
				{
					CvCreateMarkerSeries(_provider[i], _T(""), &_series[i]);
					_cvMarkerObject._spanCount = 0;
				}
				else return false;
			}

			_initialized = true;
		}
		return true;
	}

	void push(const char* name)
	{
		if(!initialize()) return;

		auto count = _cvMarkerObject._spanCount++;
		if(count >= 0 && count < MAX_DEPTH)
		{
			CvEnterSpanEx(_series[count], CvImportanceNormal, CvDefaultCategory, &_cvMarkerObject._spans[count], name);
		}
	}

	void pop()
	{
		if(!_initialized)
		{
			return;
		}

		auto count = --_cvMarkerObject._spanCount;

		if(count >= 0 && count < MAX_DEPTH)
		{
			CvLeaveSpan(_cvMarkerObject._spans[count]);
		}
	}
#else

	struct MARKER_OBJECT
	{
		PCV_MARKERSERIES _series;
		PCV_PROVIDER	 _provider;
		PCV_SPAN		 _spans[8];
		s32				 _spanCount;
	};

	GX_THREAD_LOCAL_STORAGE MARKER_OBJECT _cvMarkerObject;

	b32 initialize()
	{
		if(_cvMarkerObject._series != nullptr && _cvMarkerObject._provider != nullptr)
		{
			return true;
		}

		if(SUCCEEDED(CvInitProvider(&CvDefaultProviderGuid, &_cvMarkerObject._provider)))
		{
			CvCreateMarkerSeries(_cvMarkerObject._provider, _T(""), &_cvMarkerObject._series);
			_cvMarkerObject._spanCount = 0;

			return true;
		}

		return false;
	}

	void push(GX_CSTR name)
	{
		if(!initialize()) return;

		auto count = _cvMarkerObject._spanCount++;
		if(count >= 0 && count < ARRAY_NUM(_cvMarkerObject._spans))
		{
			CvEnterSpanEx(_cvMarkerObject._series, CvImportanceNormal, CvDefaultCategory, &_cvMarkerObject._spans[count], name);
		}
	}

	void pop()
	{
		if(!initialize()) return;

		auto count = --_cvMarkerObject._spanCount;
		GX_ASSERT(count >= 0, "過剰にpopが呼ばれている");

		if(count >= 0 && count < ARRAY_NUM(_cvMarkerObject._spans))
		{
			CvLeaveSpan(_cvMarkerObject._spans[count]);
		}
	}

#endif
}

