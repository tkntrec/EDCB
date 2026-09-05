-- TSファイルの指定位置のIフレームを取得するスクリプト
dofile(mg.script_name:gsub('[^\\/]*$','')..'util.lua')

fpath=mg.get_var(mg.request_info.query_string,'fname')
if fpath then
  fpath=DocumentToNativePath(fpath)
end
offset=GetVarInt(mg.request_info.query_string,'offset',-100000,100) or 0
ofssec=GetVarInt(mg.request_info.query_string,'ofssec',0,100000) or offset<0 and -offset

stream=nil
if fpath then
  ext=fpath:match('%.[0-9A-Za-z]+$') or ''
  extts=edcb.GetPrivateProfile('SET','TSExt','.ts','EpgTimerSrv.ini')
  -- 拡張子を限定
  if IsEqualPath(ext,extts) then
    f=edcb.io.open(fpath,'rb')
    if f then
      if ofssec then
        -- 時間シーク
        offset=0
        if ofssec~=0 then
          fsec,fsize=GetDurationSec(f)
          if SeekSec(f,ofssec,fsec,fsize) then
            offset=f:seek('cur',0) or 0
          end
        end
      else
        -- 比率シーク
        ofssec=0
        if offset~=0 then
          fsec,fsize=GetDurationSec(f)
          ofssec=math.floor(fsec*offset/100)
          if offset~=100 and SeekSec(f,ofssec,fsec,fsize) then
            offset=f:seek('cur',0) or 0
          else
            offset=math.floor(fsize*offset/100/188)*188
          end
        end
      end
      stream=GetIFrameVideoStream(f)
      f:close()
    end
  end
end

if not stream then
  mg.write(Response(404,nil,nil,0)..'\r\n')
elseif mg.request_info.request_method=='HEAD' then
  mg.write(Response(200,'application/octet-stream',nil,0)..'\r\n')
else
  mg.write(Response(200,'application/octet-stream',nil,#stream)..'\r\n')
  mg.write(stream)
end
