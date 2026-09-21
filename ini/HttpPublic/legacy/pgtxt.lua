-- メディアファイルの番組情報を取得する
dofile(mg.script_name:gsub('[^\\/]*$','')..'util.lua')

fpath=mg.get_var(mg.request_info.query_string,'fname')
if fpath then
  fname=mg.md5(fpath:upper()):sub(27)..'.program.txt'
  fpath=DocumentToNativePath(fpath)
end

code=404
if fpath and EdcbFindFilePlain(fpath) then
  ext=fpath:match('%.[0-9A-Za-z]+$') or ''
  extts=edcb.GetPrivateProfile('SET','TSExt','.ts','EpgTimerSrv.ini')
  if IsEqualPath(ext,extts) then
    -- TSファイルから抽出
    code=500
    tspgtxt=FindToolsCommand('tspgtxt')
    cmd=tspgtxt..' -o - '..QuoteCommandArgForPath(fpath)
    f=edcb.io.popen(WIN32 and '"'..cmd..'"' or cmd)
    if f then
      s=f:read('*a') or ''
      if s:find('^\xef\xbb\xbf') then
        code=200
        s=s:sub(4)
      end
      f:close()
    end
  else
    fpath=fpath:gsub('%.[0-9A-Za-z]+$','')
    if EdcbFindFilePlain(fpath..'.psc') then
      -- 書庫から抽出
      code=500
      dur=nil
      psisimux=FindToolsCommand('psisimux')
      cmd=psisimux..' -r 0 -p -y .psc -e '..QuoteCommandArgForPath(fpath..'.psc')..' -'
      f=edcb.io.popen(WIN32 and '"'..cmd..'"' or cmd,'r'..POPEN_BINARY)
      if f then
        buf=f:read(188)
        if buf and #buf==188 then
          dur=GetLeNumber(buf,25,4)
        end
        f:close()
      end
      if dur then
        tspgtxt=FindToolsCommand('tspgtxt')
        cmd=psisimux..' -m '..math.floor(dur/2)..' -y .psc -e '..QuoteCommandArgForPath(fpath..'.psc')..' - | '..tspgtxt..' -o - -'
        f=edcb.io.popen(WIN32 and '"'..cmd..'"' or cmd)
        if f then
          s=f:read('*a') or ''
          if s:find('^\xef\xbb\xbf') then
            code=200
            s=s:sub(4)
          end
          f:close()
        end
      end
    else
      -- 番組情報ファイルを返す
      f=edcb.io.open(fpath..'.program.txt','rb')
      if f then
        s=f:read('*a') or ''
        if s:find('^\xef\xbb\xbf') then
          code=200
          s=s:sub(4)
        elseif s:find('^[^\n]*～') or s:find('^[^\n]*未') then
          -- BOMはないがUTF-8っぽい
          code=200
        elseif s~='' then
          -- Shift_JIS。環境によっては変換できない
          code=500
          s=edcb.Convert('utf-8','cp932',s)
          if s and s~='' then
            code=200
          end
        end
      end
    end
  end
end

if code==200 then
  ct=CreateContentBuilder(GZIP_THRESHOLD_BYTE)
  -- 装飾済みで返す
  edcb.htmlEscape=15
  ct:Append(DecorateProgramText(EdcbHtmlEscape(s)))
  ct:Finish()
  mg.write(ct:Pop(Response(200,mg.get_mime_type(fname),'utf-8',ct.len,ct.gzip)..'Content-Disposition: filename='..fname..'\r\n\r\n'))
else
  mg.write(Response(code,nil,nil,0)..'\r\n')
end
