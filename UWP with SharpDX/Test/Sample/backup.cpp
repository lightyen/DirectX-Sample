static HRESULT CreateTextureFromDDS( _In_ ID3D11Device* d3dDevice,
                                     _In_opt_ ID3D11DeviceContext* d3dContext,
                                     _In_ const DDS_HEADER* header,
                                     _In_reads_bytes_(bitSize) const uint8_t* bitData,
                                     _In_ size_t bitSize,
                                     _In_ size_t maxsize,
                                     _In_ D3D11_USAGE usage,
                                     _In_ unsigned int bindFlags,
                                     _In_ unsigned int cpuAccessFlags,
                                     _In_ unsigned int miscFlags,
                                     _In_ bool forceSRGB,
                                     _Outptr_opt_ ID3D11Resource** texture,
                                     _Outptr_opt_ ID3D11ShaderResourceView** textureView )
{
    HRESULT hr = S_OK;

    size_t width = header->width;
    size_t height = header->height;
    size_t depth = header->depth;

    uint32_t resDim = D3D11_RESOURCE_DIMENSION_UNKNOWN;
    size_t arraySize = 1;
    DXGI_FORMAT format = DXGI_FORMAT_UNKNOWN;
    bool isCubeMap = false;

    size_t mipCount = header->mipMapCount;
    if (0 == mipCount)
    {
        mipCount = 1;
    }

    if ((header->ddspf.flags & DDS_FOURCC) &&
        (MAKEFOURCC( 'D', 'X', '1', '0' ) == header->ddspf.fourCC ))
    {
        auto d3d10ext = reinterpret_cast<const DDS_HEADER_DXT10*>( (const char*)header + sizeof(DDS_HEADER) );

        arraySize = d3d10ext->arraySize;
        if (arraySize == 0)
        {
           return HRESULT_FROM_WIN32( ERROR_INVALID_DATA );
        }

        switch( d3d10ext->dxgiFormat )
        {
        case DXGI_FORMAT_AI44:
        case DXGI_FORMAT_IA44:
        case DXGI_FORMAT_P8:
        case DXGI_FORMAT_A8P8:
            return HRESULT_FROM_WIN32( ERROR_NOT_SUPPORTED );

        default:
            if ( BitsPerPixel( d3d10ext->dxgiFormat ) == 0 )
            {
                return HRESULT_FROM_WIN32( ERROR_NOT_SUPPORTED );
            }
        }
           
        format = d3d10ext->dxgiFormat;

        switch ( d3d10ext->resourceDimension )
        {
        case D3D11_RESOURCE_DIMENSION_TEXTURE1D:
            // D3DX writes 1D textures with a fixed Height of 1
            if ((header->flags & DDS_HEIGHT) && height != 1)
            {
                return HRESULT_FROM_WIN32( ERROR_INVALID_DATA );
            }
            height = depth = 1;
            break;

        case D3D11_RESOURCE_DIMENSION_TEXTURE2D:
            if (d3d10ext->miscFlag & D3D11_RESOURCE_MISC_TEXTURECUBE)
            {
                arraySize *= 6;
                isCubeMap = true;
            }
            depth = 1;
            break;

        case D3D11_RESOURCE_DIMENSION_TEXTURE3D:
            if (!(header->flags & DDS_HEADER_FLAGS_VOLUME))
            {
                return HRESULT_FROM_WIN32( ERROR_INVALID_DATA );
            }

            if (arraySize > 1)
            {
                return HRESULT_FROM_WIN32( ERROR_NOT_SUPPORTED );
            }
            break;

        default:
            return HRESULT_FROM_WIN32( ERROR_NOT_SUPPORTED );
        }

        resDim = d3d10ext->resourceDimension;
    }
    else
    {
        format = GetDXGIFormat( header->ddspf );

        if (format == DXGI_FORMAT_UNKNOWN)
        {
           return HRESULT_FROM_WIN32( ERROR_NOT_SUPPORTED );
        }

        if (header->flags & DDS_HEADER_FLAGS_VOLUME)
        {
            resDim = D3D11_RESOURCE_DIMENSION_TEXTURE3D;
        }
        else 
        {
            if (header->caps2 & DDS_CUBEMAP)
            {
                // We require all six faces to be defined
                if ((header->caps2 & DDS_CUBEMAP_ALLFACES ) != DDS_CUBEMAP_ALLFACES)
                {
                    return HRESULT_FROM_WIN32( ERROR_NOT_SUPPORTED );
                }

                arraySize = 6;
                isCubeMap = true;
            }

            depth = 1;
            resDim = D3D11_RESOURCE_DIMENSION_TEXTURE2D;

            // Note there's no way for a legacy Direct3D 9 DDS to express a '1D' texture
        }

        assert( BitsPerPixel( format ) != 0 );
    }

    // Bound sizes (for security purposes we don't trust DDS file metadata larger than the D3D 11.x hardware requirements)
    if (mipCount > D3D11_REQ_MIP_LEVELS)
    {
        return HRESULT_FROM_WIN32( ERROR_NOT_SUPPORTED );
    }

    switch ( resDim )
    {
        case D3D11_RESOURCE_DIMENSION_TEXTURE1D:
            if ((arraySize > D3D11_REQ_TEXTURE1D_ARRAY_AXIS_DIMENSION) ||
                (width > D3D11_REQ_TEXTURE1D_U_DIMENSION) )
            {
                return HRESULT_FROM_WIN32( ERROR_NOT_SUPPORTED );
            }
            break;

        case D3D11_RESOURCE_DIMENSION_TEXTURE2D:
            if ( isCubeMap )
            {
                // This is the right bound because we set arraySize to (NumCubes*6) above
                if ((arraySize > D3D11_REQ_TEXTURE2D_ARRAY_AXIS_DIMENSION) ||
                    (width > D3D11_REQ_TEXTURECUBE_DIMENSION) ||
                    (height > D3D11_REQ_TEXTURECUBE_DIMENSION))
                {
                    return HRESULT_FROM_WIN32( ERROR_NOT_SUPPORTED );
                }
            }
            else if ((arraySize > D3D11_REQ_TEXTURE2D_ARRAY_AXIS_DIMENSION) ||
                     (width > D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION) ||
                     (height > D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION))
            {
                return HRESULT_FROM_WIN32( ERROR_NOT_SUPPORTED );
            }
            break;

        case D3D11_RESOURCE_DIMENSION_TEXTURE3D:
            if ((arraySize > 1) ||
                (width > D3D11_REQ_TEXTURE3D_U_V_OR_W_DIMENSION) ||
                (height > D3D11_REQ_TEXTURE3D_U_V_OR_W_DIMENSION) ||
                (depth > D3D11_REQ_TEXTURE3D_U_V_OR_W_DIMENSION) )
            {
                return HRESULT_FROM_WIN32( ERROR_NOT_SUPPORTED );
            }
            break;
    }

    bool autogen = false;
    if ( mipCount == 1 && d3dContext != 0 && textureView != 0 ) // Must have context and shader-view to auto generate mipmaps
    {
        // See if format is supported for auto-gen mipmaps (varies by feature level)
        UINT fmtSupport = 0;
        hr = d3dDevice->CheckFormatSupport( format, &fmtSupport );
        if ( SUCCEEDED(hr) && ( fmtSupport & D3D11_FORMAT_SUPPORT_MIP_AUTOGEN ) )
        {
            // 10level9 feature levels do not support auto-gen mipgen for volume textures
            if ( ( resDim != D3D11_RESOURCE_DIMENSION_TEXTURE3D )
                 || ( d3dDevice->GetFeatureLevel() >= D3D_FEATURE_LEVEL_10_0 ) )
            {
                autogen = true;
            }
        }
    }

    if ( autogen )
    {
        // Create texture with auto-generated mipmaps
        ID3D11Resource* tex = nullptr;
        hr = CreateD3DResources( d3dDevice, resDim, width, height, depth, 0, arraySize,
                                 format, usage,
                                 bindFlags | D3D11_BIND_RENDER_TARGET,
                                 cpuAccessFlags,
                                 miscFlags | D3D11_RESOURCE_MISC_GENERATE_MIPS, forceSRGB,
                                 isCubeMap, nullptr, &tex, textureView );
        if ( SUCCEEDED(hr) )
        {
            size_t numBytes = 0;
            size_t rowBytes = 0;
            GetSurfaceInfo( width, height, format, &numBytes, &rowBytes, nullptr );

            if ( numBytes > bitSize )
            {
                (*textureView)->Release();
                *textureView = nullptr;
                tex->Release();
                return HRESULT_FROM_WIN32( ERROR_HANDLE_EOF );
            }

            if ( arraySize > 1 )
            {
                D3D11_SHADER_RESOURCE_VIEW_DESC desc;
                (*textureView)->GetDesc( &desc );

                UINT mipLevels = 1;

                switch( desc.ViewDimension )
                {
                case D3D_SRV_DIMENSION_TEXTURE1D:       mipLevels = desc.Texture1D.MipLevels; break;
                case D3D_SRV_DIMENSION_TEXTURE1DARRAY:  mipLevels = desc.Texture1DArray.MipLevels; break;
                case D3D_SRV_DIMENSION_TEXTURE2D:       mipLevels = desc.Texture2D.MipLevels; break;
                case D3D_SRV_DIMENSION_TEXTURE2DARRAY:  mipLevels = desc.Texture2DArray.MipLevels; break;
                case D3D_SRV_DIMENSION_TEXTURECUBE:     mipLevels = desc.TextureCube.MipLevels; break;
                case D3D_SRV_DIMENSION_TEXTURECUBEARRAY:mipLevels = desc.TextureCubeArray.MipLevels; break;
                default:
                    (*textureView)->Release();
                    *textureView = nullptr;
                    tex->Release();
                    return E_UNEXPECTED;
                }

                const uint8_t* pSrcBits = bitData;
                const uint8_t* pEndBits = bitData + bitSize;
                for( UINT item = 0; item < arraySize; ++item )
                {
                    if ( (pSrcBits + numBytes) > pEndBits )
                    {
                        (*textureView)->Release();
                        *textureView = nullptr;
                        tex->Release();
                        return HRESULT_FROM_WIN32( ERROR_HANDLE_EOF );
                    }

                    UINT res = D3D11CalcSubresource( 0, item, mipLevels );
                    d3dContext->UpdateSubresource( tex, res, nullptr, pSrcBits, static_cast<UINT>(rowBytes), static_cast<UINT>(numBytes) );
                    pSrcBits += numBytes;
                }
            }
            else
            {
                d3dContext->UpdateSubresource( tex, 0, nullptr, bitData, static_cast<UINT>(rowBytes), static_cast<UINT>(numBytes) );
            }

            d3dContext->GenerateMips( *textureView );

            if ( texture )
            {
                *texture = tex;
            }
            else
            {
                tex->Release();
            }
        }
    }
    else
    {
        // Create the texture
        std::unique_ptr<D3D11_SUBRESOURCE_DATA[]> initData( new (std::nothrow) D3D11_SUBRESOURCE_DATA[ mipCount * arraySize ] );
        if ( !initData )
        {
            return E_OUTOFMEMORY;
        }

        size_t skipMip = 0;
        size_t twidth = 0;
        size_t theight = 0;
        size_t tdepth = 0;
        hr = FillInitData( width, height, depth, mipCount, arraySize, format, maxsize, bitSize, bitData,
                           twidth, theight, tdepth, skipMip, initData.get() );

        if ( SUCCEEDED(hr) )
        {
            hr = CreateD3DResources( d3dDevice, resDim, twidth, theight, tdepth, mipCount - skipMip, arraySize,
                                     format, usage, bindFlags, cpuAccessFlags, miscFlags, forceSRGB,
                                     isCubeMap, initData.get(), texture, textureView );

            if ( FAILED(hr) && !maxsize && (mipCount > 1) )
            {
                // Retry with a maxsize determined by feature level
                switch( d3dDevice->GetFeatureLevel() )
                {
                case D3D_FEATURE_LEVEL_9_1:
                case D3D_FEATURE_LEVEL_9_2:
                    if ( isCubeMap )
                    {
                        maxsize = 512 /*D3D_FL9_1_REQ_TEXTURECUBE_DIMENSION*/;
                    }
                    else
                    {
                        maxsize = (resDim == D3D11_RESOURCE_DIMENSION_TEXTURE3D)
                                  ? 256 /*D3D_FL9_1_REQ_TEXTURE3D_U_V_OR_W_DIMENSION*/
                                  : 2048 /*D3D_FL9_1_REQ_TEXTURE2D_U_OR_V_DIMENSION*/;
                    }
                    break;

                case D3D_FEATURE_LEVEL_9_3:
                    maxsize = (resDim == D3D11_RESOURCE_DIMENSION_TEXTURE3D)
                              ? 256 /*D3D_FL9_1_REQ_TEXTURE3D_U_V_OR_W_DIMENSION*/
                              : 4096 /*D3D_FL9_3_REQ_TEXTURE2D_U_OR_V_DIMENSION*/;
                    break;

                default: // D3D_FEATURE_LEVEL_10_0 & D3D_FEATURE_LEVEL_10_1
                    maxsize = (resDim == D3D11_RESOURCE_DIMENSION_TEXTURE3D)
                              ? 2048 /*D3D10_REQ_TEXTURE3D_U_V_OR_W_DIMENSION*/
                              : 8192 /*D3D10_REQ_TEXTURE2D_U_OR_V_DIMENSION*/;
                    break;
                }

                hr = FillInitData( width, height, depth, mipCount, arraySize, format, maxsize, bitSize, bitData,
                                   twidth, theight, tdepth, skipMip, initData.get() );
                if ( SUCCEEDED(hr) )
                {
                    hr = CreateD3DResources( d3dDevice, resDim, twidth, theight, tdepth, mipCount - skipMip, arraySize,
                                             format, usage, bindFlags, cpuAccessFlags, miscFlags, forceSRGB,
                                             isCubeMap, initData.get(), texture, textureView );
                }
            }
        }
    }

    return hr;
}
