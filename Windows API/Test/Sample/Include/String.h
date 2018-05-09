#pragma once
// http://nosleep.pixnet.net/blog/post/112071289-%E7%A8%8B%E5%BC%8F%E9%96%8B%E7%99%BC-%7C-vc-mfc-cstring-%E7%94%A8%E6%B3%95%E8%A9%B3%E8%A7%A3-
// https://en.wikipedia.org/wiki/C%2B%2B11#Rvalue_references_and_move_constructors
// https://msdn.microsoft.com/zh-tw/library/dd293665.aspx

#include <windows.h>

#define BUFFER_MAX_CCH 0xFFFF
#include <Strsafe.h>

#include <memory>
#include <utility>
using namespace std;

class String;

template <typename ... Arguments>
void OutputDebug(LPCTSTR format, ...)
{
#ifdef _DEBUG
	TCHAR outString[BUFFER_MAX_CCH];
	va_list argptr;
	va_start(argptr, format);
	StringCchVPrintf(outString, BUFFER_MAX_CCH, format, argptr);
	va_end(argptr);
	OutputDebugString(outString);
#endif
}

template <typename Target>
void Append(Target& target, LPCTSTR value, const size_t& size)
{

	target.append(value, size);
}

template <typename ... Arguments>
void AppendFormat(String& target, LPCTSTR format, Arguments& ... args)
{
	
	TCHAR buffer[BUFFER_MAX_CCH];
	StringCchPrintf(buffer, BUFFER_MAX_CCH, format, args ...);
	size_t len;
	if (SUCCEEDED(StringCchLength(buffer, BUFFER_MAX_CCH, &len)))
	{
		len += target.Length();
	}
	else return;
	
	target.resize(len);
	StringCchPrintf((LPTSTR)target + target.length(), BUFFER_MAX_CCH, format, args ...);
}

template <typename Target, typename ... Arguments>
void AppendFormat(Target& target, LPCTSTR format, Arguments& ... args)
{
	target(format, args ...);
}

template <typename Target, unsigned Count>
void WriteArgument(Target& target, TCHAR const (&value)[Count])
{
	AppendFormat(target, TEXT("%.*s"), value);
}

void OutputDebug()
{
	OutputDebugString(TEXT("Hello world!\n"));
}

class String
{
	unique_ptr<TCHAR>	string;
	size_t				length;

	void object_copy(const String& other)
	{
		string.reset(new TCHAR[other.length + 1]);
		length = other.length;
		StringCchCopy(string.get(), other.length + 1, other.string.get());
	}

	void object_move(String&& other) noexcept
	{
		string.reset(other.string.release());
		length = other.length;
	}

public:

	~String() = default;

	String() = default;

	// copy constructor
	String(const String& other)
	{
		//OutputDebug(TEXT("copy constructor %p %s\n"), this, other.c_str());
		object_copy(other);
	}

	// move constructor
	String(String&& other) noexcept
	{
		//OutputDebug(TEXT("move constructor %p %s %p\n"), this, other.c_str(), &other);
		object_move(forward<String>(other));
	}

	// format constructor
	template<typename... Arguments>
	String(LPCTSTR format, const Arguments&... args)
	{
		Format(format, args...);
		//OutputDebug(TEXT("format constructor %p %s\n"), this, string.get());
	}

	String(size_t length)
	{
		//OutputDebug(TEXT("custom constructor %p %u\n"), this, length);
		string.reset(new TCHAR[length + 1]);
		this->length = length;
		string.get()[length] = TEXT('\0');
	}

	String operator+ (const String& other) const
	{
		String ret(length + other.length);
		if (length) StringCchCopy(ret.string.get(), length + 1, string.get());
		if (other.length) StringCchCopy(ret.string.get() + length, other.length + 1, other.string.get());
		return ret;
	}

	String& operator=(const String& other)
	{
		//OutputDebug(TEXT("assignment operator %p=%p %s=%s\n"), this, &other, c_str(), other.c_str());
		if (this != &other)
		{
			object_copy(other);
		}
		return (*this);
	}

	String& operator= (String&& other) noexcept
	{
		if (this != &other)
		{
			object_move(forward<String>(other));
		}
		return *this;
	}

	String& operator+= (const String& other)
	{
		Append(other);
		return *this;
	}

	void resize(size_t length)
	{
		TCHAR* buf = new TCHAR[length + 1];
		size_t i;
		for (i = 0; i < this->length && i < length; i++)
		{
			buf[i] = string.get()[i];
		}
		buf[i] = TEXT('\0');
		this->length = i;
		string.reset(buf);
	}

	void Append(const String& other)
	{
		String ret(length + other.length);
		StringCchCopy(ret.string.get(), length + 1, string.get());
		StringCchCopy(ret.string.get() + length, other.length + 1, other.string.get());
		*this = ret;
	}

	bool operator<(const String& other)
	{
		if (StrCmp(c_str(), other.c_str()) < 0) return true;
		else return false;
	}

	operator LPCTSTR() const
	{
		return reinterpret_cast<LPCTSTR>(string.get());
	}

	operator LPTSTR()
	{
		return reinterpret_cast<LPTSTR>(string.get());
	}

	LPCTSTR c_str() const
	{
		return reinterpret_cast<LPCTSTR>(string.get());
	}

	size_t Length() const noexcept
	{
		return length;
	}

	bool IsNullOrEmpty() const {
		return length == 0;
	}

	void Format(LPCTSTR format, ...)
	{
		size_t remainSize;
		size_t formatSize;
		TCHAR formatString[BUFFER_MAX_CCH];

		va_list argptr;
		va_start(argptr, format);
		StringCchVPrintfEx(formatString, BUFFER_MAX_CCH, NULL,
			&remainSize, STRSAFE_FILL_BEHIND_NULL,
			format, argptr);
		va_end(argptr);

		formatSize = BUFFER_MAX_CCH - remainSize + 1;
		string.reset(new TCHAR[formatSize]);
		StringCchCopy(string.get(), formatSize, formatString);
		StringCchLength(string.get(), formatSize, &length);
	}

	String Left(size_t count) const
	{
		String ret(count);
		StringCchCopy(ret.string.get(), (count < length ? count : length) + 1, string.get());
		return ret;
	}

	int LoadString(UINT ID)
	{
		TCHAR buffer[BUFFER_MAX_CCH];
		int len = ::LoadString(GetModuleHandle(NULL), ID, buffer, BUFFER_MAX_CCH);
		string.reset(new TCHAR[len + 1]);
		StringCchCopy(string.get(), len + 1, buffer);
		return len;
	}

};
