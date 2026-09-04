@echo off
REM Hardcoded here instead of relying on user-scope env vars: a value set via
REM setx only reaches a NEW process tree (after logoff/logon or an Explorer
REM restart) -- an already-running VFP session never sees it, which is why
REM every VFP-launched run was failing "must all be set" even though these
REM same three vars were correctly present in the registry.
SET HOMSYS_BRIDGE_API_URL=http://localhost:5200
SET HOMSYS_BRIDGE_API_KEY=SET_VIA_ENVIRONMENT_VARIABLE
REM No HOMSYS_DBF_ROOT fallback here -- drunbridge always passes the real
REM data folder as %2 (derived from the already-open oowkhdr table's own
REM path), and salesorder_bridge.py's Config prefers argv[2] over this env
REM var. A hardcoded dev-machine path here would only mask %2 not being
REM passed, rather than failing loudly the way a missing dbf_root should.
REM %3 is optional -- the SO# for a single-order confirm-back, passed only
REM from the Forward-to-Invoice call. Omitted for the normal bulk call from
REM Process Orders/Form1.Init; an empty %3 is harmless (argv just stays
REM length 3 in that case, not 4).
REM %4 is optional -- the action word ("DEALLOCATE"), passed only from
REM a1112.scx's drunbridge(mdocno, "DEALLOCATE") call. Must be forwarded or
REM salesorder_bridge.py's argv[4] check can never see it and clear_oos()
REM never dispatches.
REM Form1.drunbridge (invoice.SCX) now resolves this .bat as a bare
REM "run_bridge.bat" filename -- found via VFP's own SET PATH / default
REM directory, not a SYS(16)-derived full path. Same reasoning as the
REM hardcoded vars above: this file has to ship into the live BMS program
REM folder via BMS_auto_Distribution for that bare-name lookup to work,
REM with no per-machine edit either way.
"%~dp0SalesOrderBridge.exe" %1 %2 %3 %4
