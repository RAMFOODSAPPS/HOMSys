PARAM what,wtime1,wtime2
*********
do case 
   case what = 1
	  *_time1=datetime()  &&&ThisForm.Text1.value
	  mretval = datetime()
   case what = 2   
      *_time2=datetime()   &&&ThisForm.Text2.value
      mretval = datetime()
   case what = 3
	  hours=((wtime2-wtime1)/60)/60
	  _hr1=int(hours)    &&&hour(time1)
	  _mn1=(hours-_hr1)*60   &&& minute(time1)
	  *_elapsed = str(_hr1,4)+" HR, "+str(_mn1,2)+" MIN and "+str(sec(_time2),2)+" SEC"
	  mretval = str(_hr1,4)+" HR, "+str(_mn1,2)+" MIN and "+str(sec(wtime2),2)+" SEC"
   otherwise 
      mretval = ""
ENDcase
return mretval
