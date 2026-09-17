package com.fromthefarm.app.ui.screens

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.net.Uri
import android.util.Base64
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.ByteArrayOutputStream
import java.io.File
import java.net.URL

object PhotoTools {
    fun prepare(context: Context, uri: Uri): File {
        val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        context.contentResolver.openInputStream(uri)?.use { BitmapFactory.decodeStream(it, null, bounds) }
        require(bounds.outWidth in 1..20000 && bounds.outHeight in 1..20000)
        val options = BitmapFactory.Options().apply {
            inSampleSize = 1
            while (bounds.outWidth / inSampleSize > 1024 || bounds.outHeight / inSampleSize > 1024) inSampleSize *= 2
        }
        val bitmap = context.contentResolver.openInputStream(uri)?.use { BitmapFactory.decodeStream(it, null, options) }
            ?: error("Unsupported image")
        try {
            val bytes = ByteArrayOutputStream()
            bitmap.compress(Bitmap.CompressFormat.JPEG, 70, bytes)
            require(bytes.size() <= 256 * 1024) { "Photo too large" }
            return File.createTempFile("listing-", ".jpg", context.cacheDir).apply { writeBytes(bytes.toByteArray()) }
        } finally { bitmap.recycle() }
    }
}

@Composable
fun ListingPhoto(source: String?) {
    if (source.isNullOrBlank()) return
    val bitmap by produceState<Bitmap?>(null, source) {
        value = withContext(Dispatchers.IO) {
            runCatching {
                val bytes = when {
                    source.startsWith("data:image/") -> {
                        require(source.length <= 700000)
                        Base64.decode(source.substringAfter(","), Base64.DEFAULT)
                    }
                    source.startsWith("https://") -> {
                        val connection = URL(source).openConnection().apply { connectTimeout = 10000; readTimeout = 10000 }
                        connection.getInputStream().use { input ->
                            val output = ByteArrayOutputStream()
                            val buffer = ByteArray(8192)
                            while (true) {
                                val count = input.read(buffer)
                                if (count < 0) break
                                require(output.size() + count <= 512 * 1024)
                                output.write(buffer, 0, count)
                            }
                            output.toByteArray()
                        }
                    }
                    else -> File(source).readBytes()
                }
                val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
                BitmapFactory.decodeByteArray(bytes, 0, bytes.size, bounds)
                require(bounds.outWidth in 1..20000 && bounds.outHeight in 1..20000)
                val options = BitmapFactory.Options().apply {
                    inSampleSize = 1
                    while (bounds.outWidth / inSampleSize > 1024 || bounds.outHeight / inSampleSize > 1024) inSampleSize *= 2
                }
                BitmapFactory.decodeByteArray(bytes, 0, bytes.size, options)
            }.getOrNull()
        }
    }
    val photo = bitmap
    if (photo != null) Image(photo.asImageBitmap(), contentDescription = "Listing photo",
        modifier = Modifier.fillMaxWidth().height(160.dp), contentScale = ContentScale.Crop)
    else Text("Photo loading or unavailable")
}
