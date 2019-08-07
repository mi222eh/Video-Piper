import ffmpeg from './ffmpeg/ffmpeg';
import youtubedl from './youtubedl/youtubedl';
import { ipcMain } from 'electron';

ipcMain.on('ffmpeg/combine', (event, args) => {
	ffmpeg.combine(args)
	.then((result) => {

	}).catch((err) => {
		event.sender.send({
			error:err
		});
	});;
});

ipcMain.on('ffmpeg/combine', (event, args) => {

});

ipcMain.on('youtubedl/get_info', (event, args) => {

});

ipcMain.on('youtubedl/start_video_stream', (event, args) => {

});
