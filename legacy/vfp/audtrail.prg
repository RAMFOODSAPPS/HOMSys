param p_sysmodule,p_doctype,p_docno,p_docdate,p_custkey,p_cprodno,p_amount,p_qtycs,p_qtypc,p_qty,p_action
mopenaud = openfile("audtrail")
sele audtrail
appe blank
repl sysdate with datetime(), exeid with pub_exeid, exename with getexe(), exeversion with fdate(getexe()), sysmodule with p_sysmodule
repl doctype with p_doctype, docno with p_docno, docdate with p_docdate, custkey with p_custkey, cprodno with p_cprodno
repl amount with p_amount, qtycs with p_qtycs, qtypc with p_qtypc, qty with p_qty, action with p_action
m = datauser()
m = closetab("audtrail",mopenaud)